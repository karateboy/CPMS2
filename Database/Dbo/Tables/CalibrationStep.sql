CREATE TABLE [dbo].[CalibrationStep]
(
	[Id] INT NOT NULL PRIMARY KEY IDENTITY, 
    [ConfigId] INT NOT NULL, 
    [DeviceId] INT NULL, 
    [Zero] BIT NOT NULL, 
    [Duration] INT NOT NULL, 
    [ValueTimeFromEnd] INT NOT NULL, 
    [SignalId1] INT NULL, 
    [SignalId2] INT NULL, 
    [SignalId3] INT NULL, 
    [SignalId4] INT NULL, 
    [SignalId5] INT NULL, 
    [DeviceId1] INT NULL,
    [DeviceId2] INT NULL,
    [DeviceId3] INT NULL,
    [ResetSignal] bit NOT NULL,
    CONSTRAINT [FK_CalibrationStep_ToCalibrationConfig] FOREIGN KEY ([ConfigId]) REFERENCES [CalibrationConfig]([Id]), 
    CONSTRAINT [FK_CalibrationStep_ToDevice] FOREIGN KEY ([DeviceId]) REFERENCES [Device]([Id]), 
    CONSTRAINT [FK_CalibrationStepSignal1_ToDeviceSignal] FOREIGN KEY ([SignalId1]) REFERENCES [DeviceSignal]([Id]),
    CONSTRAINT [FK_CalibrationStepSignal1_ToDeviceSignal2] FOREIGN KEY ([SignalId2]) REFERENCES [DeviceSignal]([Id]),
    CONSTRAINT [FK_CalibrationStepSignal1_ToDeviceSignal3] FOREIGN KEY ([SignalId3]) REFERENCES [DeviceSignal]([Id]),
    CONSTRAINT [FK_CalibrationStepSignal1_ToDeviceSignal4] FOREIGN KEY ([SignalId4]) REFERENCES [DeviceSignal]([Id]),
    CONSTRAINT [FK_CalibrationStepSignal1_ToDeviceSignal5] FOREIGN KEY ([SignalId5]) REFERENCES [DeviceSignal]([Id]),
    CONSTRAINT [FK_CalibrationStep_ToDevice1] FOREIGN KEY ([DeviceId1]) REFERENCES [Device]([Id]),
    CONSTRAINT [FK_CalibrationStep_ToDevice2] FOREIGN KEY ([DeviceId2]) REFERENCES [Device]([Id]),
    CONSTRAINT [FK_CalibrationStep_ToDevice3] FOREIGN KEY ([DeviceId3]) REFERENCES [Device]([Id])

)
