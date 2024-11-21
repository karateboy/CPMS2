CREATE TABLE [dbo].[AdjustedData] (
    [PipeId]     INT             NOT NULL,
    [CreateDate] DATETIME2 (7)   NOT NULL,
    [UpdateDate] DATETIME2 (7)   NOT NULL,
    PRIMARY KEY CLUSTERED ([CreateDate] ASC, [PipeId] ASC),
    CONSTRAINT [FK_FixData_ToPipe] FOREIGN KEY ([PipeId]) REFERENCES [dbo].[Pipe] ([Id])
);


