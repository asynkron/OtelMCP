using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Asynkron.OtelReceiver.Migrations
{
    /// <inheritdoc />
    public partial class LogAttributeIndexing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AttributeMap",
                table: "Logs");

            migrationBuilder.DropColumn(
                name: "ResourceMap",
                table: "Logs");

            migrationBuilder.CreateTable(
                name: "LogAttributes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LogId = table.Column<int>(type: "integer", nullable: false),
                    Key = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: false),
                    Source = table.Column<byte>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LogAttributes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LogAttributes_Logs_LogId",
                        column: x => x.LogId,
                        principalTable: "Logs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Metrics",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Proto = table.Column<byte[]>(type: "bytea", nullable: false),
                    AttributeMap = table.Column<string[]>(type: "text[]", nullable: false),
                    Unit = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    StartTimestamp = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    EndTimestamp = table.Column<decimal>(type: "numeric(20,0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Metrics", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Spans_EndTimestamp",
                table: "Spans",
                column: "EndTimestamp");

            migrationBuilder.CreateIndex(
                name: "IX_Spans_OperationName",
                table: "Spans",
                column: "OperationName");

            migrationBuilder.CreateIndex(
                name: "IX_Spans_ServiceName",
                table: "Spans",
                column: "ServiceName");

            migrationBuilder.CreateIndex(
                name: "IX_Spans_StartTimestamp",
                table: "Spans",
                column: "StartTimestamp");

            migrationBuilder.CreateIndex(
                name: "IX_LogAttributes_Key",
                table: "LogAttributes",
                column: "Key");

            migrationBuilder.CreateIndex(
                name: "IX_LogAttributes_Key_Value",
                table: "LogAttributes",
                columns: new[] { "Key", "Value" });

            migrationBuilder.CreateIndex(
                name: "IX_LogAttributes_LogId",
                table: "LogAttributes",
                column: "LogId");

            migrationBuilder.CreateIndex(
                name: "IX_LogAttributes_Value",
                table: "LogAttributes",
                column: "Value");

            migrationBuilder.CreateIndex(
                name: "IX_Metrics_Name",
                table: "Metrics",
                column: "Name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LogAttributes");

            migrationBuilder.DropTable(
                name: "Metrics");

            migrationBuilder.DropIndex(
                name: "IX_Spans_EndTimestamp",
                table: "Spans");

            migrationBuilder.DropIndex(
                name: "IX_Spans_OperationName",
                table: "Spans");

            migrationBuilder.DropIndex(
                name: "IX_Spans_ServiceName",
                table: "Spans");

            migrationBuilder.DropIndex(
                name: "IX_Spans_StartTimestamp",
                table: "Spans");

            migrationBuilder.AddColumn<string[]>(
                name: "AttributeMap",
                table: "Logs",
                type: "text[]",
                nullable: false,
                defaultValue: new string[0]);

            migrationBuilder.AddColumn<string[]>(
                name: "ResourceMap",
                table: "Logs",
                type: "text[]",
                nullable: false,
                defaultValue: new string[0]);
        }
    }
}
