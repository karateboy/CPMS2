CREATE TABLE [dbo].[DeviceOutput]
(
	[DeviceId] INT NOT NULL , 
    [OutputConfigId] INT NOT NULL, 
    [Address] INT NOT NULL, 
    [DataType] INT NOT NULL,     
    CONSTRAINT [FK_DeviceOutput_ToDevice] FOREIGN KEY ([DeviceId]) REFERENCES [Device]([Id]), 
    CONSTRAINT [PK_DeviceOutput] PRIMARY KEY ([DeviceId], [OutputConfigId])
)
