USE [EnVoQbot_DB]
GO

/****** Object:  Table [dbo].[Translations]    Script Date: 19.03.2025 10:12:21 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[Translations](
	[ID] [bigint] IDENTITY(1,1) NOT NULL,
	[WordID] [bigint] NOT NULL,
	[LanguageID] [int] NOT NULL,
	[Translation] [nvarchar](max) NOT NULL,
	[Popularity] [int] NOT NULL,
 CONSTRAINT [PK_Translations] PRIMARY KEY CLUSTERED 
(
	[ID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO

ALTER TABLE [dbo].[Translations]  WITH CHECK ADD  CONSTRAINT [FK_Translations_EnglishWords] FOREIGN KEY([WordID])
REFERENCES [dbo].[EnglishWords] ([ID])
GO

ALTER TABLE [dbo].[Translations] CHECK CONSTRAINT [FK_Translations_EnglishWords]
GO

ALTER TABLE [dbo].[Translations]  WITH CHECK ADD  CONSTRAINT [FK_Translations_Languages] FOREIGN KEY([LanguageID])
REFERENCES [dbo].[Languages] ([ID])
GO

ALTER TABLE [dbo].[Translations] CHECK CONSTRAINT [FK_Translations_Languages]
GO

