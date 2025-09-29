using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Asynkron.OtelReceiver.Migrations
{
    /// <inheritdoc />
    public partial class LogAttributes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LogAttributes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LogId = table.Column<int>(type: "integer", nullable: false),
                    Key = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: false),
                    Kind = table.Column<byte>(type: "smallint", nullable: false)
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
                name: "IX_LogAttributes_Key_Value_Kind",
                table: "LogAttributes",
                columns: new[] { "Key", "Value", "Kind" });

            migrationBuilder.CreateIndex(
                name: "IX_LogAttributes_LogId",
                table: "LogAttributes",
                column: "LogId");

            migrationBuilder.CreateIndex(
                name: "IX_Metrics_Name",
                table: "Metrics",
                column: "Name");

            if (migrationBuilder.ActiveProvider == "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                migrationBuilder.Sql(
                    """
                    INSERT INTO "LogAttributes" ("LogId", "Key", "Value", "Kind")
                    SELECT l."Id",
                           split_part(attribute, ':', 1),
                           right(attribute, length(attribute) - position(':' in attribute)),
                           0
                    FROM "Logs" AS l
                    CROSS JOIN LATERAL unnest(coalesce(l."AttributeMap", ARRAY[]::text[])) AS attribute
                    WHERE position(':' in attribute) > 0;
                    """);

                migrationBuilder.Sql(
                    """
                    INSERT INTO "LogAttributes" ("LogId", "Key", "Value", "Kind")
                    SELECT l."Id",
                           split_part(attribute, ':', 1),
                           right(attribute, length(attribute) - position(':' in attribute)),
                           1
                    FROM "Logs" AS l
                    CROSS JOIN LATERAL unnest(coalesce(l."ResourceMap", ARRAY[]::text[])) AS attribute
                    WHERE position(':' in attribute) > 0;
                    """);
            }
            else if (migrationBuilder.ActiveProvider == "Microsoft.EntityFrameworkCore.Sqlite")
            {
                migrationBuilder.Sql(
                    """
                    INSERT INTO "LogAttributes" ("LogId", "Key", "Value", "Kind")
                    SELECT l."Id",
                           substr(attribute.value, 1, instr(attribute.value, ':') - 1),
                           substr(attribute.value, instr(attribute.value, ':') + 1),
                           0
                    FROM "Logs" AS l,
                         json_each(COALESCE(l."AttributeMap", '[]')) AS attribute
                    WHERE instr(attribute.value, ':') > 0;
                    """);

                migrationBuilder.Sql(
                    """
                    INSERT INTO "LogAttributes" ("LogId", "Key", "Value", "Kind")
                    SELECT l."Id",
                           substr(attribute.value, 1, instr(attribute.value, ':') - 1),
                           substr(attribute.value, instr(attribute.value, ':') + 1),
                           1
                    FROM "Logs" AS l,
                         json_each(COALESCE(l."ResourceMap", '[]')) AS attribute
                    WHERE instr(attribute.value, ':') > 0;
                    """);
            }

            migrationBuilder.DropColumn(
                name: "AttributeMap",
                table: "Logs");

            migrationBuilder.DropColumn(
                name: "ResourceMap",
                table: "Logs");
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
