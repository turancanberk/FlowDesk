/*
  Mirrors WorkspaceSlug.SuggestFrom on the server so the address preview shown
  while typing matches what will actually be created.

  The server remains authoritative: it derives the slug itself and the response
  carries the real value. This is a preview, not the decision.
*/

const TURKISH_TRANSLITERATIONS: Readonly<Record<string, string>> = {
  ı: "i",
  I: "i",
  İ: "i",
  i: "i",
  ş: "s",
  Ş: "s",
  ğ: "g",
  Ğ: "g",
  ü: "u",
  Ü: "u",
  ö: "o",
  Ö: "o",
  ç: "c",
  Ç: "c",
};

const MAXIMUM_LENGTH = 40;

export function suggestSlug(name: string): string {
  const transliterated = Array.from(name)
    .map((character) => TURKISH_TRANSLITERATIONS[character] ?? character)
    .join("");

  const ascii = transliterated
    // Strips accents left on other Latin letters, e.g. "é" to "e".
    .normalize("NFD")
    .replace(/\p{Mn}/gu, "")
    .toLowerCase();

  const slug = ascii.replace(/[^a-z0-9]+/g, "-").replace(/^-+|-+$/g, "");

  return slug.length > MAXIMUM_LENGTH ? slug.slice(0, MAXIMUM_LENGTH).replace(/-+$/, "") : slug;
}
