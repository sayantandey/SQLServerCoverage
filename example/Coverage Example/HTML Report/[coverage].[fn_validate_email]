
CREATE FUNCTION coverage.fn_validate_email(
    @email NVARCHAR(100)
)
RETURNS BIT
AS
BEGIN
    RETURN CASE
        WHEN @email LIKE '%_@__%.__%' 
             AND CHARINDEX('@', @email) > 1
             AND LEN(@email) - LEN(REPLACE(@email, '@', '')) = 1
        THEN 1
        ELSE 0
    END;
END;


