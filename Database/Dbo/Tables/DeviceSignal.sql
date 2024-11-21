CREATE TABLE [dbo].[DeviceSignal]
(
	[Id] INT NOT NULL PRIMARY KEY IDENTITY,
	[DeviceId] INT NOT NULL , 
	[Name] NVARCHAR(50) NOT NULL, 
	[PipeId] INT NOT NULL, 
	[Coil] BIT NOT NULL , 
    [Address] INT NOT NULL,        
    [Offset] INT NOT NULL DEFAULT 0, 
    CONSTRAINT [FK_DeviceSignal_ToDevice] FOREIGN KEY ([DeviceId]) REFERENCES [Device]([Id]),
)

GO

CREATE INDEX [IX_DeviceSignal_Column] ON [dbo].[DeviceSignal] ([DeviceId])
