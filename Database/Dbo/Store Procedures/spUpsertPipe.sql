CREATE PROCEDURE [dbo].[spUpsertPipe]
	@Id INT,
	@Name NVARCHAR(50), 
    @EpaCode NVARCHAR(50),
    @Area DECIMAL(18, 3), 
    @BaseO2 DECIMAL(18, 2), 
    @LightDiameter DECIMAL(18, 3), 
    @EmissionDiameter DECIMAL(18, 3), 
    @LastNormalOzone DECIMAL(18, 3), 
    @NormalOzoneTime DATETIME2
AS
begin
	set nocount on;
    Update Pipe
    Set Name = @Name, EpaCode = @EpaCode, Area = @Area, BaseO2 = @BaseO2, LightDiameter = @LightDiameter, EmissionDiameter = @EmissionDiameter, LastNormalOzone = @LastNormalOzone, NormalOzoneTime = @NormalOzoneTime
	Where Id = @Id
    If @@RowCount = 0
    begin
        Insert Into Pipe (Name, EpaCode, Area, BaseO2, LightDiameter, EmissionDiameter, LastNormalOzone, NormalOzoneTime)
        Values (@Name, @EpaCode, @Area, @BaseO2, @LightDiameter, @EmissionDiameter, @LastNormalOzone, @NormalOzoneTime)
    end
end


