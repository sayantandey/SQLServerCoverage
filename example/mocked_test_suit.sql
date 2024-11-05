-- Create the coverage testing database if it doesn't exist
IF NOT EXISTS (
    SELECT *
    FROM sys.databases
    WHERE name = 'sql_coverage_test'
)
BEGIN
    CREATE DATABASE sql_coverage_test;
END
GO

USE sql_coverage_test;
GO

-- Enable advanced options for testing
EXEC sp_configure 'show advanced options', 1;
GO
RECONFIGURE;
GO

-- Create schema for organizing objects
IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'coverage')
    EXEC('CREATE SCHEMA coverage');
GO
IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'exclude')
    EXEC('CREATE SCHEMA exclude');
GO

-- Drop existing objects if they exist
IF OBJECT_ID('coverage.fn_get_current_timestamp') IS NOT NULL DROP FUNCTION coverage.fn_get_current_timestamp;
IF OBJECT_ID('coverage.fn_calculate_age') IS NOT NULL DROP FUNCTION coverage.fn_calculate_age;
IF OBJECT_ID('coverage.fn_validate_email') IS NOT NULL DROP FUNCTION coverage.fn_validate_email;
IF OBJECT_ID('coverage.sp_process_employee') IS NOT NULL DROP PROCEDURE coverage.sp_process_employee;
IF OBJECT_ID('coverage.sp_audit_log') IS NOT NULL DROP PROCEDURE coverage.sp_audit_log;
IF OBJECT_ID('coverage.sp_complex_business_logic') IS NOT NULL DROP PROCEDURE coverage.sp_complex_business_logic;
IF OBJECT_ID('exclude.sp_maintenance') IS NOT NULL DROP PROCEDURE exclude.sp_maintenance;
IF OBJECT_ID('exclude.sp_cleanup') IS NOT NULL DROP PROCEDURE exclude.sp_cleanup;
IF OBJECT_ID('coverage.Employees') IS NOT NULL DROP TABLE coverage.Employees;
IF OBJECT_ID('coverage.AuditLog') IS NOT NULL DROP TABLE coverage.AuditLog;
GO

-- Create supporting tables
CREATE TABLE coverage.Employees (
    EmployeeID INT IDENTITY(1,1) PRIMARY KEY,
    FirstName NVARCHAR(50),
    LastName NVARCHAR(50),
    Email NVARCHAR(100),
    BirthDate DATE,
    Department NVARCHAR(50),
    Salary DECIMAL(18,2),
    IsActive BIT DEFAULT 1,
    CreatedDate DATETIME DEFAULT GETDATE(),
    ModifiedDate DATETIME
);

CREATE TABLE coverage.AuditLog (
    LogID INT IDENTITY(1,1) PRIMARY KEY,
    EventType NVARCHAR(50),
    ObjectName NVARCHAR(100),
    EventDetails NVARCHAR(MAX),
    UserName NVARCHAR(100),
    EventDate DATETIME DEFAULT GETDATE()
);
GO

-- Create utility functions
CREATE FUNCTION coverage.fn_get_current_timestamp()
RETURNS DATETIME
AS
BEGIN
    RETURN GETDATE();
END;
GO

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
GO

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
GO

-- Create core stored procedures
CREATE PROCEDURE coverage.sp_audit_log
    @eventType NVARCHAR(50),
    @objectName NVARCHAR(100),
    @eventDetails NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON;
    
    INSERT INTO coverage.AuditLog (EventType, ObjectName, EventDetails, UserName)
    VALUES (@eventType, @objectName, @eventDetails, SYSTEM_USER);
    
    -- Test different execution paths
    IF @eventType = 'ERROR'
    BEGIN
        RAISERROR ('An error occurred during audit logging', 16, 1);
        RETURN;
    END
    
    IF @eventType = 'WARNING'
    BEGIN
        PRINT 'Warning event logged';
    END
    ELSE
    BEGIN
        PRINT 'Normal event logged';
    END
END;
GO

CREATE PROCEDURE coverage.sp_process_employee
    @firstName NVARCHAR(50),
    @lastName NVARCHAR(50),
    @email NVARCHAR(100),
    @birthDate DATE,
    @department NVARCHAR(50),
    @salary DECIMAL(18,2),
    @operation CHAR(1) -- 'C' for Create, 'U' for Update, 'D' for Delete
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Input validation with multiple conditions
    IF @operation NOT IN ('C', 'U', 'D')
    BEGIN
        THROW 50001, 'Invalid operation type specified', 1;
        RETURN;
    END

    -- Email validation using function
    IF coverage.fn_validate_email(@email) = 0
    BEGIN
        THROW 50002, 'Invalid email format', 1;
        RETURN;
    END

    -- Age validation using function
    IF coverage.fn_calculate_age(@birthDate) < 18
    BEGIN
        THROW 50003, 'Employee must be at least 18 years old', 1;
        RETURN;
    END

    BEGIN TRY
        -- Different operation paths
        IF @operation = 'C'
        BEGIN
            INSERT INTO coverage.Employees (
                FirstName, LastName, Email, BirthDate, 
                Department, Salary, CreatedDate
            )
            VALUES (
                @firstName, @lastName, @email, @birthDate,
                @department, @salary, coverage.fn_get_current_timestamp()
            );

            DECLARE @createDetails NVARCHAR(MAX) = N'Created employee: ' + @firstName + N' ' + @lastName;
            EXEC coverage.sp_audit_log 
                @eventType = 'INSERT',
                @objectName = 'Employees',
                @eventDetails = @createDetails;
        END
        ELSE IF @operation = 'U'
        BEGIN
            UPDATE coverage.Employees
            SET FirstName = @firstName,
                LastName = @lastName,
                Email = @email,
                Department = @department,
                Salary = @salary,
                ModifiedDate = coverage.fn_get_current_timestamp()
            WHERE Email = @email;

            IF @@ROWCOUNT = 0
            BEGIN
                THROW 50004, 'Employee not found for update', 1;
            END

            DECLARE @updateDetails NVARCHAR(MAX) = N'Updated employee: ' + @email;
            EXEC coverage.sp_audit_log 
                @eventType = 'UPDATE',
                @objectName = 'Employees',
                @eventDetails = @updateDetails;
        END
        ELSE -- Delete operation
        BEGIN
            DELETE FROM coverage.Employees
            WHERE Email = @email;

            IF @@ROWCOUNT = 0
            BEGIN
                THROW 50005, 'Employee not found for deletion', 1;
            END

            DECLARE @deleteDetails NVARCHAR(MAX) = N'Deleted employee: ' + @email;
            EXEC coverage.sp_audit_log 
                @eventType = 'DELETE',
                @objectName = 'Employees',
                @eventDetails = @deleteDetails;
        END
    END TRY
    BEGIN CATCH
        DECLARE @errorMessage NVARCHAR(4000) = ERROR_MESSAGE();
        EXEC coverage.sp_audit_log 
            @eventType = 'ERROR',
            @objectName = 'Employees',
            @eventDetails = @errorMessage;
        THROW;
    END CATCH
END;
GO

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
GO

-- Create procedures to be excluded from coverage
CREATE PROCEDURE exclude.sp_maintenance
AS
BEGIN
    SET NOCOUNT ON;
    PRINT 'Performing system maintenance';
    -- Maintenance logic here
END;
GO

CREATE PROCEDURE exclude.sp_cleanup
AS
BEGIN
    SET NOCOUNT ON;
    PRINT 'Performing system cleanup';
    -- Cleanup logic here
END;
GO

-- Insert sample data
INSERT INTO coverage.Employees (FirstName, LastName, Email, BirthDate, Department, Salary)
VALUES 
    ('John', 'Doe', 'john.doe@example.com', '1990-01-15', 'IT', 75000.00),
    ('Jane', 'Smith', 'jane.smith@example.com', '1985-03-20', 'HR', 65000.00),
    ('Bob', 'Johnson', 'bob.johnson@example.com', '1988-07-10', 'IT', 80000.00);
GO