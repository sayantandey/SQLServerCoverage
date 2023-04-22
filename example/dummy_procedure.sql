IF NOT EXISTS (
		SELECT *
		FROM sys.databases
		WHERE name = 'sql_server_coverage'
		)
BEGIN
	CREATE DATABASE sql_server_coverage
END
GO

USE sql_server_coverage
GO

DROP FUNCTION IF EXISTS udf_test_function
GO

CREATE FUNCTION udf_test_function()
RETURNS DATETIME
AS
BEGIN
	RETURN GETDATE()
END

GO

DROP PROCEDURE IF EXISTS sp_test_proc
GO
CREATE PROCEDURE sp_test_proc
AS
BEGIN
	PRINT 'test stored procedure'
	print CURRENT_USER
	PRINT dbo.udf_test_function()
END
GO


DROP PROCEDURE IF EXISTS sp_sql_server_coverage
GO
CREATE PROCEDURE sp_sql_server_coverage @arg NUMERIC(1, 0)
AS
BEGIN
	IF @arg = 2
	BEGIN
		PRINT ('If condition')
	END
	ELSE
	BEGIN
		EXEC sp_test_proc
	END
END
GO

-- procedure to be excuded from coverage 
DROP PROCEDURE IF EXISTS sp_dummy_proc1
GO
CREATE PROCEDURE sp_dummy_proc1
AS
BEGIN
	print 'sp_dummy_proc1'
	END
	Go


DROP PROCEDURE IF EXISTS sp_dummy_proc1
GO
CREATE PROCEDURE sp_dummy_proc1
AS
BEGIN
	print 'sp_dummy_proc1'
	END
	Go


DROP PROCEDURE IF EXISTS sp_dummy_proc1
GO
CREATE PROCEDURE sp_dummy_proc1
AS
BEGIN
	print 'sp_dummy_proc1'
	END
Go
	