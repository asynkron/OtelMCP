using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Asynkron.OtelReceiver.Migrations
{
    /// <inheritdoc />
    public partial class SpanAttributeNormalization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SpanAttributeValues",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SpanId = table.Column<string>(type: "text", nullable: false),
                    Key = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: false),
                    Source = table.Column<byte>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpanAttributeValues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SpanAttributeValues_Spans_SpanId",
                        column: x => x.SpanId,
                        principalTable: "Spans",
                        principalColumn: "SpanId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SpanAttributeValues_Key",
                table: "SpanAttributeValues",
                column: "Key");

            migrationBuilder.CreateIndex(
                name: "IX_SpanAttributeValues_Key_Value",
                table: "SpanAttributeValues",
                columns: new[] { "Key", "Value" });

            migrationBuilder.CreateIndex(
                name: "IX_SpanAttributeValues_SpanId",
                table: "SpanAttributeValues",
                column: "SpanId");

            migrationBuilder.CreateIndex(
                name: "IX_SpanAttributeValues_Value",
                table: "SpanAttributeValues",
                column: "Value");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SpanAttributeValues");
        }
    }
}
