-- 1. What do cataract-related Level3 names look like?
SELECT DISTINCT ID, Code, Level3, Level1
FROM TPAProcedures
WHERE Deleted = 0 AND Level3 LIKE '%cataract%'
ORDER BY Level3;

-- 2. What do maternity/delivery Level3 names look like?
SELECT DISTINCT ID, Code, Level3, Level1
FROM TPAProcedures
WHERE Deleted = 0 AND (Level3 LIKE '%maternit%' OR Level3 LIKE '%delivery%'
   OR Level3 LIKE '%LSCS%' OR Level3 LIKE '%caesar%' OR Level3 LIKE '%pregnan%')
ORDER BY Level3;
