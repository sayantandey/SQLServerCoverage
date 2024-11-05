
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


