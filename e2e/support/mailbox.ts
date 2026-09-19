import { expect } from "@playwright/test";
import { readDotEnv } from "./environment";

/*
  The development Mailpit, reached over its HTTP API. The worker the suite
  starts sends real mail through it, so a test can follow an invitation from
  the inbox the way the invited person would.
*/
const mailpitUrl = `http://localhost:${readDotEnv()["MAILPIT_UI_PORT"] ?? "8025"}`;

interface MailpitSummary {
  ID: string;
  Subject: string;
  To: { Address: string }[];
}

/**
 * Waits for the first message to <paramref name="address"/> and returns its
 * HTML body. Each test addresses a fresh mailbox, so the first message is the
 * one the test caused.
 */
export async function waitForMailTo(address: string): Promise<{ subject: string; html: string }> {
  let found: MailpitSummary | undefined;

  await expect
    .poll(
      async () => {
        const response = await fetch(
          `${mailpitUrl}/api/v1/search?query=${encodeURIComponent(`to:"${address}"`)}`,
        );
        const inbox = (await response.json()) as { messages: MailpitSummary[] };

        found = inbox.messages[0];

        return found !== undefined;
      },
      { message: `${address} adresine e-posta gelmedi`, timeout: 30_000 },
    )
    .toBe(true);

  const message = (await (await fetch(`${mailpitUrl}/api/v1/message/${found?.ID}`)).json()) as {
    HTML: string;
  };

  return { subject: found?.Subject ?? "", html: message.HTML };
}

/** The first link in a mail body that points at <paramref name="path"/>. */
export function linkTo(html: string, path: string): string {
  const escaped = path.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
  const match = new RegExp(`href="([^"]*${escaped}[^"]*)"`).exec(html);

  if (match?.[1] === undefined) {
    throw new Error(`E-postada ${path} bağlantısı yok.`);
  }

  // Mail bodies escape ampersands; the browser needs the real URL.
  return match[1].replaceAll("&amp;", "&");
}
