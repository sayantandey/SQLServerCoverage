
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


