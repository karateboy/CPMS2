CREATE TABLE [dbo].[Pipe]
(
	[Id] INT NOT NULL PRIMARY KEY, 
    [Name] NVARCHAR(50) NOT NULL, 
    [EpaCode] NVARCHAR(50) NOT NULL,
    [Area] DECIMAL(18, 3) NOT NULL, 
    [BaseO2] DECIMAL(18, 2) NOT NULL, 
    [LightDiameter] DECIMAL(18, 3) NOT NULL, 
    [EmissionDiameter] DECIMAL(18, 3) NOT NULL, 
    [LastNormalOzone] DECIMAL(18, 3) NOT NULL, 
    [NormalOzoneTime] DATETIME2 NOT NULL, 
    [LastNormalTemp] DECIMAL(18, 3) NOT NULL,
    [NormalTempTime] DATETIME2 NOT NULL, 
    [UpperSource] NVARCHAR(50) NOT NULL DEFAULT '', 
    [AutoLoadState] BIT NOT NULL DEFAULT 0, 
    [StopThreshold] DECIMAL(18, 3) NULL 
)
