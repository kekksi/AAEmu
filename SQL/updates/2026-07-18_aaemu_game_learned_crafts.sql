USE aaemu_game;

CREATE TABLE IF NOT EXISTS `learned_crafts` (
  `owner` INT UNSIGNED NOT NULL COMMENT 'Character who learned the craft',
  `craft_id` INT UNSIGNED NOT NULL COMMENT 'Craft template ID from compact data',
  `learned_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`owner`, `craft_id`) USING BTREE
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Craft recipes learned by characters';
