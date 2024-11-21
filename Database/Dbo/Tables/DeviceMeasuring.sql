CREATE TABLE [dbo].[DeviceMeasuring]
(
	[DeviceId] INT NOT NULL , 
	[PipeId] INT NOT NULL, 
    [Sid] NVARCHAR(50) NOT NULL,     
	[InputReg] BIT NOT NULL , 
    [Address] INT NOT NULL, 
    [DataType] INT NOT NULL, 
    [MidEndian] BIT NOT NULL DEFAULT 0, 
    CONSTRAINT [FK_DeviceMeasuring_ToDevice] FOREIGN KEY ([DeviceId]) REFERENCES [Device]([Id]),
    CONSTRAINT [FK_DeviceMeasuring_ToMonitorType] FOREIGN KEY ([PipeId], [Sid]) REFERENCES [MonitorType]([PipeId], [Sid]), 
    CONSTRAINT [PK_DeviceMeasuring] PRIMARY KEY ([PipeId], [Sid]) 
)
