CREATE TABLE [dbo].[MonitorType]
(
	[PipeId] INT NOT NULL , 
    [Sid] NVARCHAR(50) NOT NULL, 
    [Name] NVARCHAR(50) NOT NULL, 
    [Unit] NVARCHAR(50) NOT NULL, 
    [Unit2] NVARCHAR(50) NOT NULL, 
    [Upload] BIT NOT NULL, 
    [Standard] DECIMAL(18, 3) NULL, 
    [Standard4Stop] DECIMAL(18, 3) NULL, 
    [Alarm] DECIMAL(18, 3) NULL, 
    [Warning] DECIMAL(18, 3) NULL, 
    [AlarmLow] DECIMAL(18, 3) NULL, 
    [WarningLow] DECIMAL(18, 3) NULL, 
    [EmissionFactor] DECIMAL(18, 3) NULL, 
    [ControlEfficiency] DECIMAL(18, 3) NULL, 
    [OverrideState] NVARCHAR(3) NOT NULL DEFAULT '' , 
    [SrcState] NVARCHAR(3) NOT NULL DEFAULT 'N', 
    [Order] INT NOT NULL DEFAULT 1, 
    [MaxValue] DECIMAL(18, 3) NULL, 
    [MonitorOtherPipe] NVARCHAR(50) NULL, 
    PRIMARY KEY ([PipeId], [Sid]), 
    CONSTRAINT [FK_MonitorType_ToPipeId] FOREIGN KEY ([PipeId]) REFERENCES [Pipe]([Id])
)
