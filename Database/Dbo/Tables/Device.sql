CREATE TABLE [dbo].[Device]
(
	[Id] INT NOT NULL PRIMARY KEY Identity(1,1), 
    [Name] NVARCHAR(50) NOT NULL, 
    [PipeId] INT NOT NULL , 
    [ModbusAddress] NVARCHAR(50) NOT NULL, 
    [Port] INT NOT NULL, 
    [SlaveId] INT NOT NULL, 
    [Spare] BIT NOT NULL, 
    [Authenticated] BIT NOT NULL,
    [BigEndian] BIT NOT NULL DEFAULT 1, 
    [Output] BIT NOT NULL DEFAULT 0, 
    CONSTRAINT [FK_Device_ToPipeId] FOREIGN KEY ([PipeId]) REFERENCES [Pipe]([Id])
)
