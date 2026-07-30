using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocuMind.Infrastructure.Migrations
{
    /// <summary>
    /// !!! DESTRUCTIVE — <see cref="Down"/> CANNOT RESTORE THE DELETED ROWS !!!
    ///
    /// <para>
    /// <see cref="Up"/> starts by deleting every existing row in <c>document_chunks</c> and
    /// <c>documents</c>, because the new <c>OwnerId</c> columns on both tables are <c>NOT NULL</c>
    /// and no pre-Phase-2 row has an owner to backfill from — there is no authenticated caller to
    /// attribute a pre-existing document to. This is a deliberate, approved, one-time data loss for
    /// this project's current data (4 documents, 4 chunks at the time this migration was authored),
    /// not an oversight. Re-ingest content after applying this migration; the three fixture PDFs
    /// under <c>backend/tests/DocuMind.UnitTests/Fixtures/</c> make that a matter of seconds.
    /// </para>
    /// <para>
    /// The delete is intentionally part of THIS migration rather than a prerequisite the operator
    /// runs by hand first: a fresh clone applying every migration in order must reproduce the exact
    /// same schema and behaviour a hand-truncated environment gets, and a document uploaded between
    /// "truncate" and "add NOT NULL column" as two separate migrations would make this migration
    /// fail outright on `dotnet ef database update` (a NOT NULL column cannot be added under a row
    /// with no value to give it). Combining both steps in one migration removes that window
    /// entirely.
    /// </para>
    /// <para>
    /// <see cref="Down"/> below only drops the columns/keys/constraints this migration added — it
    /// has no way to know what rows existed before <see cref="Up"/> ran, so those rows are gone for
    /// good once this migration has been applied.
    /// </para>
    /// </summary>
    /// <inheritdoc />
    public partial class AddDocumentOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // MUST run first, before the NOT NULL OwnerId columns are added below: every existing
            // row in both tables predates authentication and has no owner to assign, and the
            // all-zero placeholder EF would otherwise need for the AddColumn step does not exist in
            // AspNetUsers, so the AddForeignKey step at the end of this method would fail outright
            // against a populated table. See the loud class-level comment above.
            migrationBuilder.Sql("DELETE FROM document_chunks; DELETE FROM documents;");

            migrationBuilder.DropForeignKey(
                name: "FK_document_chunks_documents_DocumentId",
                table: "document_chunks");

            migrationBuilder.DropIndex(
                name: "IX_document_chunks_DocumentId",
                table: "document_chunks");

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerId",
                table: "documents",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerId",
                table: "document_chunks",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddUniqueConstraint(
                name: "AK_documents_Id_OwnerId",
                table: "documents",
                columns: new[] { "Id", "OwnerId" });

            migrationBuilder.CreateIndex(
                name: "IX_documents_OwnerId",
                table: "documents",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_document_chunks_DocumentId_OwnerId",
                table: "document_chunks",
                columns: new[] { "DocumentId", "OwnerId" });

            migrationBuilder.AddForeignKey(
                name: "FK_document_chunks_documents_DocumentId_OwnerId",
                table: "document_chunks",
                columns: new[] { "DocumentId", "OwnerId" },
                principalTable: "documents",
                principalColumns: new[] { "Id", "OwnerId" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_documents_AspNetUsers_OwnerId",
                table: "documents",
                column: "OwnerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_document_chunks_documents_DocumentId_OwnerId",
                table: "document_chunks");

            migrationBuilder.DropForeignKey(
                name: "FK_documents_AspNetUsers_OwnerId",
                table: "documents");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_documents_Id_OwnerId",
                table: "documents");

            migrationBuilder.DropIndex(
                name: "IX_documents_OwnerId",
                table: "documents");

            migrationBuilder.DropIndex(
                name: "IX_document_chunks_DocumentId_OwnerId",
                table: "document_chunks");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "document_chunks");

            migrationBuilder.CreateIndex(
                name: "IX_document_chunks_DocumentId",
                table: "document_chunks",
                column: "DocumentId");

            migrationBuilder.AddForeignKey(
                name: "FK_document_chunks_documents_DocumentId",
                table: "document_chunks",
                column: "DocumentId",
                principalTable: "documents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
