-- What UHID does OUR ClaimAI lookup get? (MemberPolicy via claim)
SELECT mp.UHIDNO AS OurUHID
FROM Claims c WITH (NOLOCK)
JOIN MemberPolicy mp WITH (NOLOCK) ON mp.ID = c.MemberPolicyID
WHERE CAST(c.ID AS VARCHAR(50)) = '<CLAIM_ID>' AND ISNULL(c.Deleted,0)=0;

-- Does that UHID actually return history rows from the SP?
EXEC USP_ClaimHistory_Retrieve @Uhidno = '<the OurUHID value from above>';
