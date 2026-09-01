using HelpDesk.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace HelpDesk.Infrastructure.Data.Migrations;

/// <summary>
/// Fixes deployments where the hand-written AI-control migration
/// (20260828120000_AddAiControlFields) was never discovered/applied because it
/// lacked the [Migration] attribute. Adds the AI columns only if missing, so it is
/// safe on both fresh and already-migrated databases.
/// </summary>
[DbContext(typeof(HelpDeskDbContext))]
[Migration("20260831220000_AddMissedAiColumns")]
public partial class AddMissedAiColumns : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
IF COL_LENGTH(N'Tickets', N'AiModeEnabled') IS NULL
    ALTER TABLE [Tickets] ADD [AiModeEnabled] bit NOT NULL DEFAULT CAST(1 AS bit);
IF COL_LENGTH(N'Tickets', N'WaitingForAgent') IS NULL
    ALTER TABLE [Tickets] ADD [WaitingForAgent] bit NOT NULL DEFAULT CAST(0 AS bit);
IF COL_LENGTH(N'Tickets', N'IsAiReply') IS NULL
    ALTER TABLE [Tickets] ADD [IsAiReply] bit NOT NULL DEFAULT CAST(0 AS bit);
IF COL_LENGTH(N'Tickets', N'EscalationReason') IS NULL
    ALTER TABLE [Tickets] ADD [EscalationReason] nvarchar(500) NULL;
IF COL_LENGTH(N'Messages', N'IsAiGenerated') IS NULL
    ALTER TABLE [Messages] ADD [IsAiGenerated] bit NOT NULL DEFAULT CAST(0 AS bit);
");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
IF COL_LENGTH(N'Tickets', N'AiModeEnabled') IS NOT NULL
    ALTER TABLE [Tickets] DROP COLUMN [AiModeEnabled];
IF COL_LENGTH(N'Tickets', N'WaitingForAgent') IS NOT NULL
    ALTER TABLE [Tickets] DROP COLUMN [WaitingForAgent];
IF COL_LENGTH(N'Tickets', N'IsAiReply') IS NOT NULL
    ALTER TABLE [Tickets] DROP COLUMN [IsAiReply];
IF COL_LENGTH(N'Tickets', N'EscalationReason') IS NOT NULL
    ALTER TABLE [Tickets] DROP COLUMN [EscalationReason];
IF COL_LENGTH(N'Messages', N'IsAiGenerated') IS NOT NULL
    ALTER TABLE [Messages] DROP COLUMN [IsAiGenerated];
");
    }
}