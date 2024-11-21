CREATE TABLE [dbo].[Alarm]
(
	[Id] BIGINT NOT NULL PRIMARY KEY IDENTITY, 
    [CreateDate] DATETIME2 NOT NULL, 
    [Level] INT NOT NULL, 
    [Message] NVARCHAR(50) NOT NULL
)

GO

CREATE INDEX [IX_Alarm_CreateDate_Level] ON [dbo].[Alarm] ([CreateDate], [Level])

GO


CREATE INDEX [IX_Alarm_Message] ON [dbo].[Alarm] ([Message])
