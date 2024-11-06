
CREATE FUNCTION coverage.fn_calculate_age(
    @birthDate DATE
)
RETURNS INT
AS
BEGIN
    RETURN DATEDIFF(YEAR, @birthDate, GETDATE()) -
        CASE
            WHEN (MONTH(@birthDate) > MONTH(GETDATE())) OR
                 (MONTH(@birthDate) = MONTH(GETDATE()) AND DAY(@birthDate) > DAY(GETDATE()))
            THEN 1
            ELSE 0
        END;
END;


