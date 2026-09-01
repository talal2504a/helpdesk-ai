using Microsoft.EntityFrameworkCore.Migrations;

namespace HelpDesk.Infrastructure.Data.Migrations;

public partial class AddAiControlFields : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "AiModeEnabled",
            table: "Tickets",
            type: "bit",
            nullable: false,
            defaultValue: true);

        migrationBuilder.AddColumn<bool>(
            name: "WaitingForAgent",
            table: "Tickets",
            type: "bit",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<bool>(
            name: "IsAiReply",
            table: "Tickets",
            type: "bit",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<string>(
            name: "EscalationReason",
            table: "Tickets",
            type: "nvarchar(500)",
            maxLength: 500,
            nullable: true);

        migrationBuilder.AddColumn<bool>(
            name: "IsAiGenerated",
            table: "Messages",
            type: "bit",
            nullable: false,
            defaultValue: false);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "AiModeEnabled", table: "Tickets");
        migrationBuilder.DropColumn(name: "WaitingForAgent", table: "Tickets");
        migrationBuilder.DropColumn(name: "EscalationReason", table: "Tickets");
        migrationBuilder.DropColumn(name: "IsAiGenerated", table: "Messages");
    }
}
