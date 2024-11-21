CREATE TABLE [dbo].[RawData] (
    [PipeId]     INT             NOT NULL,
    [CreateDate] DATETIME2 (7)   NOT NULL,
    [UpdateDate] DATETIME2 (7)   NOT NULL,
    [Water]      DECIMAL (18, 2) NULL,
    PRIMARY KEY CLUSTERED ([CreateDate] ASC, [PipeId] ASC),
    CONSTRAINT [FK_RawData_ToPipe] FOREIGN KEY ([PipeId]) REFERENCES [dbo].[Pipe] ([Id])
);


