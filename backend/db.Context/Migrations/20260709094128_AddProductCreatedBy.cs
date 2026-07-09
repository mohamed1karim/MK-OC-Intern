using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace db.Context.Migrations
{
    /// <inheritdoc />
    public partial class AddProductCreatedBy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CreatedByUserId",
                table: "products",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_products_CreatedByUserId",
                table: "products",
                column: "CreatedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_products_users_CreatedByUserId",
                table: "products",
                column: "CreatedByUserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_products_users_CreatedByUserId",
                table: "products");

            migrationBuilder.DropIndex(
                name: "IX_products_CreatedByUserId",
                table: "products");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "products");
        }
    }
}
