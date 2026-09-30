using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Freeside.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The group roles come from Database/bootstrap-roles.sql, run once by an admin.
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'freeside_migrator')
                        OR NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'freeside_app') THEN
                        RAISE EXCEPTION 'Roles freeside_migrator and freeside_app are missing. Run src/Freeside.Infrastructure/Database/bootstrap-roles.sql as an admin first.';
                    END IF;
                END
                $$;
                """);

            migrationBuilder.CreateTable(
                name: "ledger_entries",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    entry_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    subject_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    subject_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    previous_state = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    next_state = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    amount_msat = table.Column<long>(type: "bigint", nullable: true),
                    source = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    source_event_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    actor = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    correlation_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    idempotency_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    fiat_amount_minor = table.Column<long>(type: "bigint", nullable: true),
                    fiat_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ledger_entries", x => x.id);
                    table.CheckConstraint("ck_ledger_entries_amount_msat", "amount_msat >= 0");
                    table.CheckConstraint("ck_ledger_entries_fiat_amount_minor", "fiat_amount_minor >= 0");
                    table.CheckConstraint("ck_ledger_entries_fiat_currency", "fiat_currency ~ '^[A-Z]{3}$'");
                    table.CheckConstraint("ck_ledger_entries_fiat_pair", "(fiat_amount_minor IS NULL) = (fiat_currency IS NULL)");
                });

            migrationBuilder.CreateIndex(
                name: "ix_ledger_entries_subject_type_subject_id",
                table: "ledger_entries",
                columns: new[] { "subject_type", "subject_id" });

            migrationBuilder.CreateIndex(
                name: "ux_ledger_entries_idempotency_key",
                table: "ledger_entries",
                column: "idempotency_key",
                unique: true);

            // Append-only (AGENTS.md §2, invariant 7), enforced twice: the app role may only
            // SELECT and INSERT, and triggers reject UPDATE, DELETE and TRUNCATE for everyone,
            // the owner included. Only a superuser can disable the triggers, and even then the
            // app role still lacks the privileges.
            migrationBuilder.Sql("""
                ALTER TABLE ledger_entries OWNER TO freeside_migrator;
                REVOKE ALL ON TABLE ledger_entries FROM PUBLIC;
                GRANT SELECT, INSERT ON TABLE ledger_entries TO freeside_app;

                CREATE FUNCTION ledger_entries_reject_change() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION 'ledger_entries is append-only: % is not allowed', TG_OP
                        USING ERRCODE = 'restrict_violation';
                END
                $$;
                ALTER FUNCTION ledger_entries_reject_change() OWNER TO freeside_migrator;

                CREATE TRIGGER ledger_entries_no_update_or_delete
                    BEFORE UPDATE OR DELETE ON ledger_entries
                    FOR EACH ROW EXECUTE FUNCTION ledger_entries_reject_change();
                CREATE TRIGGER ledger_entries_no_truncate
                    BEFORE TRUNCATE ON ledger_entries
                    FOR EACH STATEMENT EXECUTE FUNCTION ledger_entries_reject_change();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ledger_entries");

            migrationBuilder.Sql("DROP FUNCTION ledger_entries_reject_change();");
        }
    }
}
