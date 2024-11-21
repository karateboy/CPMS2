CREATE TABLE [dbo].[CalibrationDevice]
(
    [Id] INT NOT NULL IDENTITY,
    [PipeId] INT NOT NULL , 
    [CreateDate] DATETIME2 NOT NULL,	
    [Sid] NVARCHAR(50) NOT NULL,     
    [DeviceType] INT NOT NULL, 
    [Zero] DECIMAL(18, 3) NOT NULL, 
    [ZeroEffective] DATETIME2 NOT NULL, 
    [Span] DECIMAL(18, 3) NOT NULL, 
    [SpanEffective] DATETIME2 NOT NULL,             
    CONSTRAINT [FK_CalibrationDevice_ToPipe] FOREIGN KEY ([PipeId]) REFERENCES [Pipe]([Id]), 
    CONSTRAINT [PK_CalibrationDevice] PRIMARY KEY ([Id])
)

GO


CREATE INDEX [IX_CalibrationDevice_ZeroEffective] ON [dbo].[CalibrationDevice] ([PipeId], [Sid], [ZeroEffective])

GO

CREATE INDEX [IX_CalibrationDevice_SpanEffective] ON [dbo].[CalibrationDevice] ([PipeId], [Sid], [SpanEffective])

GO

CREATE INDEX [IX_CalibrationDevice_PipeCreateDate] ON [dbo].[CalibrationDevice] ([PipeId], [CreateDate])
