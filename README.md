SELECT TOP 20
        c.ID, tp.Level1 AS Specialty,
        CASE WHEN tp.Level1 = 'Ophthalmology' THEN 'cataract'
             WHEN tp.Level1 IN ('Obstetrics and Gynecology','OBG') THEN 'maternity'
             ELSE 'other' END AS Decision
FROM        Claims c WITH (NOLOCK)
LEFT JOIN   ClaimsCoding cc WITH (NOLOCK) ON cc.ClaimID = c.ID
LEFT JOIN   TPAProcedures tp WITH (NOLOCK) ON tp.ID = cc.TPALevel3
WHERE       ISNULL(c.Deleted,0)=0 AND tp.Level1 IS NOT NULL
ORDER BY    c.ID DESC;
