CREATE TABLE [dbo].[AdjustedData60] (
    [PipeId]     INT             NOT NULL,
    [CreateDate] DATETIME2 (7)   NOT NULL,
    [UpdateDate] DATETIME2 (7)   NOT NULL,
    PRIMARY KEY CLUSTERED ([PipeId] ASC, [CreateDate] ASC),
    CONSTRAINT [FK_FixData60_ToPipe] FOREIGN KEY ([PipeId]) REFERENCES [dbo].[Pipe] ([Id])
);


