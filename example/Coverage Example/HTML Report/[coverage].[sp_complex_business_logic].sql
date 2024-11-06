
-- Create complex business logic procedure with multiple paths
CREATE PROCEDURE coverage.sp_complex_business_logic
    @department NVARCHAR(50),
    @salaryAdjustment DECIMAL(5,2),
    @actionType CHAR(1)
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Variable declarations for different paths
    DECLARE @totalEmployees INT;
    DECLARE @avgSalary DECIMAL(18,2);
    DECLARE @maxSalary DECIMAL(18,2);
    
    -- Initialize variables with department statistics
    SELECT 
        @totalEmployees = COUNT(*),
        @avgSalary = AVG(Salary),
        @maxSalary = MAX(Salary)
    FROM coverage.Employees
    WHERE Department = @department
        AND IsActive = 1;
    
    -- Multiple execution paths based on conditions
    IF @totalEmployees = 0
    BEGIN
        THROW 50006, 'No active employees found in specified department', 1;
        RETURN;
    END
    
    -- Nested conditions for different business scenarios
    IF @actionType = 'A' -- Adjust salaries
    BEGIN
        IF @salaryAdjustment > 10.00
        BEGIN
            THROW 50007, 'Salary adjustment cannot exceed 10%', 1;
            RETURN;
        END
        
        IF @avgSalary * (1 + @salaryAdjustment/100) > @maxSalary
        BEGIN
            PRINT 'Warning: Adjustment will exceed historical maximum salary';
        END
        
        UPDATE coverage.Employees
        SET Salary = Salary * (1 + @salaryAdjustment/100),
            ModifiedDate = coverage.fn_get_current_timestamp()
        WHERE Department = @department
            AND IsActive = 1;
            
        DECLARE @adjustDetails NVARCHAR(MAX) = N'Applied ' + CAST(@salaryAdjustment AS NVARCHAR(10)) + N'% salary adjustment to ' + @department;
        EXEC coverage.sp_audit_log 
            @eventType = 'SALARY_ADJUSTMENT',
            @objectName = 'Employees',
            @eventDetails = @adjustDetails;
    END
    ELSE IF @actionType = 'R' -- Generate report
    BEGIN
        SELECT 
            Department,
            COUNT(*) as EmployeeCount,
            AVG(Salary) as AvgSalary,
            MIN(Salary) as MinSalary,
            MAX(Salary) as MaxSalary
        FROM coverage.Employees
        WHERE Department = @department
            AND IsActive = 1
        GROUP BY Department;
            
        DECLARE @reportDetails NVARCHAR(MAX) = N'Generated department report for ' + @department;
        EXEC coverage.sp_audit_log 
            @eventType = 'REPORT_GENERATED',
            @objectName = 'Employees',
            @eventDetails = @reportDetails;
    END
    ELSE
    BEGIN
        THROW 50008, 'Invalid action type specified', 1;
    END
END;


