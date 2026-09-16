/**
 * A failure returned by the API as RFC 9457 ProblemDetails.
 */
export class ApiError extends Error {
  constructor(
    /** HTTP status code. */
    readonly status: number,
    /** Stable machine-readable code, e.g. `auth.invalid_credentials`. */
    readonly code: string | null,
    /** Turkish text safe to show the user. */
    message: string,
    /** Field-level validation messages, keyed by field name. */
    readonly fieldErrors: Readonly<Record<string, string[]>> = {},
  ) {
    super(message);
    this.name = "ApiError";
  }

  get isUnauthorized(): boolean {
    return this.status === 401;
  }

  get hasFieldErrors(): boolean {
    return Object.keys(this.fieldErrors).length > 0;
  }
}

type ProblemDetails = {
  title?: unknown;
  detail?: unknown;
  code?: unknown;
  errors?: unknown;
};

/**
 * Builds an {@link ApiError} from a failed response.
 *
 * Every message shown to the user comes from the server so the wording stays in
 * one place. When the body is unreadable — a proxy error page, a dropped
 * connection — a Turkish fallback is used rather than surfacing raw text the
 * user cannot act on.
 */
export async function toApiError(response: Response): Promise<ApiError> {
  let problem: ProblemDetails = {};

  try {
    problem = (await response.json()) as ProblemDetails;
  } catch {
    // Body was missing or not JSON; the fallback message covers it.
  }

  const message =
    typeof problem.detail === "string" && problem.detail.length > 0
      ? problem.detail
      : typeof problem.title === "string" && problem.title.length > 0
        ? problem.title
        : "Beklenmeyen bir hata oluştu. Lütfen tekrar deneyin.";

  const code = typeof problem.code === "string" ? problem.code : null;

  return new ApiError(response.status, code, message, parseFieldErrors(problem.errors));
}

function parseFieldErrors(errors: unknown): Record<string, string[]> {
  if (errors === null || typeof errors !== "object") {
    return {};
  }

  const parsed: Record<string, string[]> = {};

  for (const [field, messages] of Object.entries(errors as Record<string, unknown>)) {
    if (Array.isArray(messages)) {
      parsed[field] = messages.filter((message): message is string => typeof message === "string");
    }
  }

  return parsed;
}
