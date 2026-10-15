using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameGuild.API.Database.Migrations;

public partial class RegisterBountyPostingTemplatesInWriter : Migration
{
    private static void InstallBountyPostingTemplateValidation(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE OR REPLACE FUNCTION economy_private.validate_bounty_claim_posting_lines_v1(p_lines jsonb)
            RETURNS boolean
            LANGUAGE plpgsql
            IMMUTABLE
            STRICT
            SET search_path = pg_catalog, economy_private
            AS $function$
            DECLARE
                first_line jsonb;
                second_line jsonb;
                currency integer;
            BEGIN
                IF jsonb_typeof(p_lines) <> 'array' OR jsonb_array_length(p_lines) <> 2 THEN
                    RETURN false;
                END IF;

                IF EXISTS (
                    SELECT 1
                    FROM jsonb_array_elements(p_lines) AS line
                    WHERE NULLIF(line->>'id', '')::uuid IS NULL
                       OR NULLIF(line->>'account_id', '')::uuid IS NULL
                       OR COALESCE((line->>'amount_units')::bigint, 0) <= 0
                       OR COALESCE((line->>'side')::integer, 0) NOT IN (1, 2)
                       OR COALESCE((line->>'currency')::integer, 0) NOT IN (1, 2)
                       OR COALESCE((line->>'account_code')::integer, 0) NOT BETWEEN 1 AND 15) THEN
                    RETURN false;
                END IF;

                first_line := p_lines->0;
                second_line := p_lines->1;
                currency := (first_line->>'currency')::integer;

                RETURN (first_line->>'amount_units')::bigint = (second_line->>'amount_units')::bigint
                   AND (
                       (currency = 1
                        AND economy_private.line_matches_v2(first_line, 1, 9, 1, false, NULL)
                        AND economy_private.line_matches_v2(second_line, 2, 3, 1, true, 2))
                       OR
                       (currency = 2
                        AND economy_private.line_matches_v2(first_line, 1, 10, 2, false, NULL)
                        AND economy_private.line_matches_v2(second_line, 2, 4, 2, true, 7))
                   );
            END
            $function$;

            CREATE OR REPLACE FUNCTION economy_private.validate_bounty_reclaim_posting_lines_v1(p_lines jsonb)
            RETURNS boolean
            LANGUAGE plpgsql
            IMMUTABLE
            STRICT
            SET search_path = pg_catalog, economy_private
            AS $function$
            DECLARE
                line_count integer;
                pair_index integer;
                currency integer;
                escrow_account integer;
                fee_account integer;
                debit_line jsonb;
                credit_line jsonb;
                credit_provenance integer;
                credit_account integer;
            BEGIN
                IF jsonb_typeof(p_lines) <> 'array' THEN
                    RETURN false;
                END IF;

                line_count := jsonb_array_length(p_lines);
                IF line_count < 2 OR line_count % 2 <> 0 THEN
                    RETURN false;
                END IF;

                currency := (p_lines->0->>'currency')::integer;
                IF currency NOT IN (1, 2) THEN
                    RETURN false;
                END IF;
                escrow_account := CASE currency WHEN 1 THEN 9 ELSE 10 END;
                fee_account := CASE currency WHEN 1 THEN 14 ELSE 6 END;

                IF EXISTS (
                    SELECT 1
                    FROM jsonb_array_elements(p_lines) AS line
                    WHERE NULLIF(line->>'id', '')::uuid IS NULL
                       OR NULLIF(line->>'account_id', '')::uuid IS NULL
                       OR COALESCE((line->>'amount_units')::bigint, 0) <= 0
                       OR COALESCE((line->>'side')::integer, 0) NOT IN (1, 2)
                       OR COALESCE((line->>'currency')::integer, 0) <> currency
                       OR COALESCE((line->>'account_code')::integer, 0) NOT BETWEEN 1 AND 15) THEN
                    RETURN false;
                END IF;

                FOR pair_index IN 0..(line_count / 2 - 1) LOOP
                    debit_line := p_lines->(pair_index * 2);
                    credit_line := p_lines->(pair_index * 2 + 1);
                    IF NOT economy_private.line_matches_v2(
                        debit_line, 1, escrow_account, currency, false, NULL) THEN
                        RETURN false;
                    END IF;
                    IF (debit_line->>'amount_units')::bigint <>
                       (credit_line->>'amount_units')::bigint THEN
                        RETURN false;
                    END IF;

                    IF NULLIF(credit_line->>'wallet_id', '') IS NULL THEN
                        IF pair_index <> line_count / 2 - 1
                           OR NOT economy_private.line_matches_v2(
                               credit_line, 2, fee_account, currency, false, NULL) THEN
                            RETURN false;
                        END IF;
                        CONTINUE;
                    END IF;

                    credit_provenance := NULLIF(credit_line->>'provenance', '')::integer;
                    credit_account := (credit_line->>'account_code')::integer;
                    IF credit_provenance IS NULL
                       OR (credit_line->>'side')::integer <> 2
                       OR (credit_line->>'currency')::integer <> currency THEN
                        RETURN false;
                    END IF;
                    IF currency = 1 AND NOT (
                        credit_provenance BETWEEN 1 AND 7
                        AND ((credit_provenance = 2 AND credit_account = 3)
                             OR (credit_provenance <> 2 AND credit_account = 2))) THEN
                        RETURN false;
                    END IF;
                    IF currency = 2 AND NOT (
                        credit_provenance BETWEEN 3 AND 7 AND credit_account = 4) THEN
                        RETURN false;
                    END IF;
                END LOOP;

                RETURN true;
            END
            $function$;

            ALTER FUNCTION economy_private.validate_posting_lines_v1(integer,jsonb)
                RENAME TO validate_posting_lines_legacy_v1;

            CREATE FUNCTION economy_private.validate_posting_lines_v1(
                p_template_kind integer,
                p_lines jsonb)
            RETURNS boolean
            LANGUAGE sql
            IMMUTABLE
            STRICT
            SET search_path = pg_catalog, economy_private
            AS $function$
                SELECT CASE p_template_kind
                    WHEN 22 THEN economy_private.validate_bounty_escrow_posting_lines_v1(p_lines)
                    WHEN 23 THEN economy_private.validate_bounty_claim_posting_lines_v1(p_lines)
                    WHEN 24 THEN economy_private.validate_bounty_reclaim_posting_lines_v1(p_lines)
                    ELSE economy_private.validate_posting_lines_legacy_v1(p_template_kind, p_lines)
                END
            $function$;

            ALTER FUNCTION economy_private.validate_bounty_claim_posting_lines_v1(jsonb)
                OWNER TO gameguild_economy_procedure_owner;
            ALTER FUNCTION economy_private.validate_bounty_reclaim_posting_lines_v1(jsonb)
                OWNER TO gameguild_economy_procedure_owner;
            ALTER FUNCTION economy_private.validate_posting_lines_v1(integer,jsonb)
                OWNER TO gameguild_economy_procedure_owner;
            REVOKE ALL ON FUNCTION economy_private.validate_bounty_claim_posting_lines_v1(jsonb) FROM PUBLIC;
            REVOKE ALL ON FUNCTION economy_private.validate_bounty_reclaim_posting_lines_v1(jsonb) FROM PUBLIC;
            REVOKE ALL ON FUNCTION economy_private.validate_posting_lines_v1(integer,jsonb) FROM PUBLIC;
            """);
    }

    private static void RemoveBountyPostingTemplateValidation(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DROP FUNCTION economy_private.validate_posting_lines_v1(integer,jsonb);
            ALTER FUNCTION economy_private.validate_posting_lines_legacy_v1(integer,jsonb)
                RENAME TO validate_posting_lines_v1;
            DROP FUNCTION economy_private.validate_bounty_reclaim_posting_lines_v1(jsonb);
            DROP FUNCTION economy_private.validate_bounty_claim_posting_lines_v1(jsonb);
            """);
    }
}
