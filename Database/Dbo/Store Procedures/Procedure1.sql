CREATE PROCEDURE [dbo].[Procedure1]
	@param1 int = 0,
	@param2 int
AS
begin
	-- SET NOCOUNT ON added to prevent extra result sets from
	set nocount on;
	SELECT @param1, @param2
	set nocount off;
end
