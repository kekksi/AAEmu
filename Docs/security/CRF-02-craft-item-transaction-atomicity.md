# CRF-02: Craft item transaction atomicity

Status: design only; no production fix is included.

Baseline: `auroria` at `c09d4a6e4afffde29f3dc38fae7c2617fcd0aa6f`.

## Root cause

The request path is:

1. `CSExecuteCraft.Read()` accepts a valid recipe and calls `character.Craft.Craft(...)`
   (`AAEmu.Game/Core/Packets/C2G/CSExecuteCraft.cs:10-37`).
2. `CharacterCraft.Craft()` checks an unlocked inventory snapshot at
   `AAEmu.Game/Models/Game/Char/CharacterCraft.cs:109-117`, overwrites the shared
   `CurrentCraft`, `Count`, and `DoodadId` state at lines 96-98, sets `IsCrafting`
   without rejecting an already-running craft at line 170, then starts the skill at
   line 203.
3. `CraftEffect.Apply()` calls `character.Craft.EndCraft()` from the skill effect
   (`AAEmu.Game/Models/Game/Skills/Effects/CraftEffect.cs:41-90`, and line 147 for the
   fallback interaction path).
4. `CharacterCraft.EndCraft()` creates the product first at
   `AAEmu.Game/Models/Game/Char/CharacterCraft.cs:332-370`, then consumes each material
   separately at lines 372-375.

Both sides are fallible and neither return value is handled:

- The normal product call at `CharacterCraft.cs:359` ignores the boolean returned by
  `ItemContainer.AcquireDefaultItem()`. That implementation can return `false` for an
  invalid template, insufficient capacity, or failed item creation
  (`ItemContainer.cs:620-633` and `:681-685`), and it can throw after earlier mutations
  (`:729-736`). Multi-product recipes can therefore leave only a prefix of their
  products before all materials are consumed.
- Each material call at `CharacterCraft.cs:374` ignores the count returned by
  `ItemContainer.ConsumeItem()`. `ConsumeItem()` explicitly consumes as much as it can,
  mutates stacks incrementally, and returns the partial count (`ItemContainer.cs:504-581`).
  A later missing material does not restore an earlier consumed material.
- `ItemContainer` exposes its mutable `List<Item>` and its relevant read/add/remove/
  consume methods have no common lock. The capacity check and mutations are therefore
  not isolated from a second craft, split, move, destroy, trade, or save operation.

### Reproducible interleavings

Second-request duplicate with one recipe worth of materials:

1. Request A and request B both pass the material snapshot at `CharacterCraft.cs:109-117`.
2. Both skills reach `EndCraft()` because `Craft()` never rejects `IsCrafting == true`.
3. A adds the product at line 359; B adds the product at line 359.
4. A consumes the materials at line 374.
5. B consumes zero or a partial count at line 374; the ignored return does not reject or
   roll back B's product.

Material-loss/partial-result window:

1. A product acquisition returns false or a multi-product recipe acquires only its first
   product.
2. Execution continues because the normal acquisition result is ignored.
3. The material loop consumes all available materials. If a later material is missing,
   earlier material rows remain consumed because each call mutates independently.

A disconnect does not coordinate with this lifecycle. The hard-disconnect path marks the
character offline and saves/removes it (`GameConnection.cs:77-103`), but it neither cancels
`CharacterCraft` nor waits for an item transaction. `SaveAndRemoveFromWorld()` only invokes
`Character.SaveDirectlyToDatabase()` (`GameConnection.cs:199-233`); item persistence is
performed separately by `ItemManager.Save()` during a `SaveManager` transaction
(`SaveManager.cs:77-97`). The database transaction is atomic, but it provides no isolation
from the unlocked in-memory product/material sequence, so a save can observe a before/after
mixture.

### Compact evidence

`CraftManager.Load()` reads `craft_products` into `CraftProducts` at
`AAEmu.Game/Core/Managers/CraftManager.cs:53-79` and `craft_materials` into
`CraftMaterials` at lines 81-103. Against the shipped 1.2
`Data/compact.sqlite3`:

```text
craft_id  skill_id  wi_id  material_id  material_amount  product_id  product_amount
2         16766     41     4747         1                24920       1
2         16766     41     8006         100              24920       1
```

The database contains 7,010 crafts, 23,475 material rows, and 6,938 product rows.
5,639 crafts have more than one material row, so partial material consumption is not a
theoretical edge case. Five crafts also have more than one product row.

The product-before-material order was introduced by `db5573ef` to avoid the earlier
"crafting and missing Items" failure. Reversing that order without a real rollback would
reintroduce the known material-loss mode.

## Required minimal transaction design

The existing MNY-01 pattern (`d90f0bca`) proves atomic balance mutation by validating and
applying under one lock. LAB-01 (`c7501da2`) adds an explicit reserve/commit/release
lifecycle. Crafting needs the same semantics, but at inventory scope and with exact item
identity.

### 1. One inventory transaction gate

Add one transaction gate owned by `Inventory`, shared by Bag and Equipment. Every operation
that reads or mutates player item counts, slots, or stacks must use that gate. The item save
snapshot must use the same gate. A lock used only by `CharacterCraft` is insufficient because
other packet paths mutate the same containers.

Lock ordering must be documented and enforced for multi-inventory operations such as trade:
acquire inventory gates by stable character/container ID, then the ItemManager registry lock.

### 2. Reserve without mutation

`TryReserveCraft(craft, outputPlan)` runs under the inventory gate and returns a single-use
token. It must:

- reject a second active craft token;
- resolve exact material slices `(item object/id, original version/count, reserved count)`,
  including the grade-bearing material;
- make reserved quantities unavailable to every other consumer without changing counts,
  slots, dirty flags, IDs, packets, quests, or acquisition/consumption callbacks;
- validate every product template and precompute placement against the post-consumption bag
  layout, including stack merges, multiple products, and the backpack/trade-pack slot;
- reserve the output slots/stack capacity used by that plan.

Failure or disconnect before commit only releases the token. Since reservation has not
mutated items, this path cannot lose materials.

### 3. Commit with an exact undo journal

`CommitCraft(token)` reacquires the same gate, verifies that the token is pending and its
exact item versions still match, then applies the precomputed plan using internal mutation
primitives that do not emit packets, callbacks, quest events, or expose dirty state midway.

The undo journal must capture original objects and every changed field, not template/count
approximations: item ID and registry membership, owner/container/slot, count, grade, flags,
expiry, UCC, charge fields, crafter data, and dirty state. Any exception or failed invariant
restores those exact objects and releases every newly allocated ID before the gate opens.
Re-acquiring default material items is not an acceptable rollback.

Only after all material removals and product additions succeed may the transaction:

1. change the token atomically from pending to committed;
2. expose all dirty item state to `ItemManager.Save()`;
3. release reservations;
4. emit one coherent item-task result and the deferred inventory callbacks;
5. fire the craft quest event and schedule the next iteration.

The existing labor reservation must be associated with the same craft attempt. If labor
commit can still fail, item commit must not be published until labor can be committed, or the
item undo journal must remain available to roll back that failure.

### 4. Disconnect and persistence contract

- Disconnect while pending: cancel the craft and release reservations; inventory is unchanged.
- Disconnect racing commit: wait on the inventory gate; observe either the complete state
  before commit or the complete state after commit.
- Save racing commit: `ItemManager.Save()` must take an inventory-consistent snapshot under
  the same gate, so its existing MySQL transaction persists all material and product changes
  together or neither.
- Repeated effect callback or repeated commit: the single-use token returns the stored outcome
  and performs no second mutation.

## Tests required before enabling the fix

The implementation is not safe to merge without deterministic failure injection and these
tests (in addition to the existing `CharacterCraftTests`):

1. 1,000 concurrent commits using one recipe worth of materials: exactly one succeeds; the
   exact material delta and product delta occur once.
2. Two concurrent `CSExecuteCraft` attempts: the second cannot overwrite the first attempt's
   recipe/count/doodad state.
3. Disconnect before commit: reservation is released and every material object/field is
   byte-for-byte unchanged; no product exists.
4. Disconnect racing commit: the result is exactly pre-commit or post-commit, never mixed.
5. Injected failure after each material mutation, each stack merge, each product creation,
   ItemManager registration, and immediately before publish: exact rollback, no leaked ID,
   no packet/callback/quest event.
6. Save racing every commit barrier: the saved rows are always the complete before or complete
   after state.
7. Compact recipe 2 (two materials), all five multi-product recipes, a full/fragmented bag,
   grade inheritance, expiring/UCC/charged equipment material, and trade-pack auto-equip.
8. Repeated `EndCraft`/effect callback and repeated commit-token use are idempotent.

## Why this commit contains no code fix

The repository currently has neither the shared inventory transaction gate nor reversible,
side-effect-free item mutation primitives. Adding only an `IsCrafting` check, swapping the
operation order, or compensating with `AcquireDefaultItem()` cannot prove rollback fidelity
and leaves save/disconnect and non-craft inventory races open. Under CRF-02's no-item-loss
constraint, shipping any of those partial fixes would be riskier than leaving the exploit
explicitly documented for a proper transaction implementation.

No SQL or compact migration is required by this design.
