using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Asynkron.OtelReceiver.Migrations
{
    /// <inheritdoc />
    public partial class SpanAttributeIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SpanAttributeIndex",
                columns: table => new
                {
                    SpanId = table.Column<string>(type: "text", nullable: false),
                    Key = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpanAttributeIndex", x => new { x.SpanId, x.Key, x.Value });
                    table.ForeignKey(
                        name: "FK_SpanAttributeIndex_Spans_SpanId",
                        column: x => x.SpanId,
                        principalTable: "Spans",
                        principalColumn: "SpanId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SpanAttributeIndex_Key_Value",
                table: "SpanAttributeIndex",
                columns: new[] { "Key", "Value" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SpanAttributeIndex");
        }
    }
}
