using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Common.Migrations
{
    public partial class Addedfieldstooutboxevent : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ExpirationDate",
                table: "OutboxEvents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastRetryDate",
                table: "OutboxEvents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PublishingDate",
                table: "OutboxEvents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Retries",
                table: "OutboxEvents",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "OutboxEvents",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExpirationDate",
                table: "OutboxEvents");

            migrationBuilder.DropColumn(
                name: "LastRetryDate",
                table: "OutboxEvents");

            migrationBuilder.DropColumn(
                name: "PublishingDate",
                table: "OutboxEvents");

            migrationBuilder.DropColumn(
                name: "Retries",
                table: "OutboxEvents");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "OutboxEvents");
        }
    }
}
