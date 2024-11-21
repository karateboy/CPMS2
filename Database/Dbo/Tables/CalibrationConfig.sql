CREATE TABLE [dbo].[CalibrationConfig]
(
	[Id] INT NOT NULL PRIMARY KEY IDENTITY, 
    [PipeId] INT NOT NULL,
    [Name] NVARCHAR(50) NOT NULL, 
    [CalibrationTime] TIME NOT NULL, 
    [Period] INT NOT NULL, 
    [Enabled] BIT NOT NULL, 
    CONSTRAINT [FK_CalibrationConfig_ToPipe] FOREIGN KEY ([PipeId]) REFERENCES [Pipe]([Id])
)
