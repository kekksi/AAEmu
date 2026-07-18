-- Fix invalid 1.2 starter-quest step skills. These values resolve to NPC rows,
-- not skill rows, and therefore must not be executed as component skills.
UPDATE quest_components
SET skill_id = 0
WHERE id = 8971 AND quest_context_id = 1933 AND skill_id = 13590;

UPDATE quest_components
SET skill_id = 0
WHERE id = 5668 AND quest_context_id = 1245 AND skill_id = 11981;

UPDATE quest_components
SET skill_id = 0
WHERE id = 1326 AND quest_context_id = 25 AND skill_id = 11212;

UPDATE quest_components
SET skill_id = 0
WHERE id = 5062 AND quest_context_id = 1033 AND skill_id = 13737;
