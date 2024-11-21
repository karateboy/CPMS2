CREATE TABLE [dbo].[Calibration]
(   [PipeId] INT NOT NULL,
    [Sid] NVARCHAR(50) NOT NULL, 
    [StartTime] DATETIME2 NOT NULL, 
    [EndTime] DATETIME2 NOT NULL, 
    [ZeroValue] DECIMAL(18, 3) NOT NULL, 
    [ZeroStandard] DECIMAL(18, 3) NOT NULL, 
    [SpanValue] DECIMAL(18, 3) NOT NULL, 
    [SpanStandard] DECIMAL(18, 3) NOT NULL, 
    [Failed] INT NOT NULL, 
    CONSTRAINT [PK_Calibration] PRIMARY KEY ([PipeId], [Sid], [StartTime]), 
    CONSTRAINT [FK_Calibration_ToPipe] FOREIGN KEY ([PipeId]) REFERENCES [Pipe]([Id]) 
)

GO
