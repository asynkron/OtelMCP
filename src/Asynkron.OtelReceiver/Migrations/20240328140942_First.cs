using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Asynkron.OtelReceiver.Migrations
{
    /// <inheritdoc />
    public partial class First : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ComponentMetaData",
                columns: table => new
                {
                    NamePath = table.Column<string>(type: "text", nullable: false),
                    Annotation = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComponentMetaData", x => x.NamePath);
                });

            migrationBuilder.CreateTable(
                name: "Logs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Timestamp = table.Column<long>(type: "bigint", nullable: false),
                    ObservedTimestamp = table.Column<long>(type: "bigint", nullable: false),
                    TraceId = table.Column<string>(type: "text", nullable: false),
                    SpanId = table.Column<string>(type: "text", nullable: false),
                    SeverityText = table.Column<string>(type: "text", nullable: false),
                    SeverityNumber = table.Column<byte>(type: "smallint", nullable: false),
                    Body = table.Column<string>(type: "text", nullable: false),
                    RawBody = table.Column<string>(type: "text", nullable: false),
                    Proto = table.Column<byte[]>(type: "bytea", nullable: false),
                    ResourceMap = table.Column<string[]>(type: "text[]", nullable: false),
                    AttributeMap = table.Column<string[]>(type: "text[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Logs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SpanAttributes",
                columns: table => new
                {
                    Key = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpanAttributes", x => new { x.Key, x.Value });
                });

            migrationBuilder.CreateTable(
                name: "SpanNames",
                columns: table => new
                {
                    ServiceName = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpanNames", x => new { x.ServiceName, x.Name });
                });

            migrationBuilder.CreateTable(
                name: "Spans",
                columns: table => new
                {
                    SpanId = table.Column<string>(type: "text", nullable: false),
                    StartTimestamp = table.Column<long>(type: "bigint", nullable: false),
                    EndTimestamp = table.Column<long>(type: "bigint", nullable: false),
                    TraceId = table.Column<string>(type: "text", nullable: false),
                    ParentSpanId = table.Column<string>(type: "text", nullable: false),
                    ServiceName = table.Column<string>(type: "text", nullable: false),
                    OperationName = table.Column<string>(type: "text", nullable: false),
                    Events = table.Column<string[]>(type: "text[]", nullable: false),
                    Proto = table.Column<byte[]>(type: "bytea", nullable: false),
                    AttributeMap = table.Column<string[]>(type: "text[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Spans", x => x.SpanId);
                });

            migrationBuilder.CreateTable(
                name: "UserSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Theme = table.Column<string>(type: "text", nullable: false),
                    TimestampType = table.Column<byte>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSettings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Logs_TraceId",
                table: "Logs",
                column: "TraceId");

            migrationBuilder.CreateIndex(
                name: "IX_Spans_SpanId",
                table: "Spans",
                column: "SpanId");

            migrationBuilder.CreateIndex(
                name: "IX_Spans_TraceId",
                table: "Spans",
                column: "TraceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ComponentMetaData");

            migrationBuilder.DropTable(
                name: "Logs");

            migrationBuilder.DropTable(
                name: "SpanAttributes");

            migrationBuilder.DropTable(
                name: "SpanNames");

            migrationBuilder.DropTable(
                name: "Spans");

            migrationBuilder.DropTable(
                name: "UserSettings");
        }
    }
}
