import { API_URL } from "./environment";

/*
  Arranges state through the API, so each test spends the browser only on the
  journey it is about. Registering three people through the form before every
  test would make the suite slow, and would test the registration form thirty
  times without adding anything.
*/

export const PASSWORD = "gecerli-parola-2026";

export type Role = "Owner" | "Admin" | "Agent" | "Viewer";

export interface Person {
  id: string;
  email: string;
  displayName: string;
  accessToken: string;
}

export interface Workspace {
  id: string;
  slug: string;
  name: string;
}

/** A fresh address per call: tests share one database. */
export function uniqueEmail(prefix: string): string {
  return `${prefix}.${Date.now()}.${Math.random().toString(36).slice(2, 8)}@ornek.test`;
}

/** A name that is unique, so a test can find its own row in a list. */
export function uniqueName(base: string): string {
  return `${base} ${Math.random().toString(36).slice(2, 7)}`;
}

export async function register(displayName: string, prefix = "e2e"): Promise<Person> {
  const email = uniqueEmail(prefix);

  const session = await send<{ accessToken: string; user: { id: string } }>(
    "POST",
    "/api/auth/register",
    { email, displayName, password: PASSWORD },
  );

  return { id: session.user.id, email, displayName, accessToken: session.accessToken };
}

export function createWorkspace(owner: Person, name = uniqueName("E2E Alanı")): Promise<Workspace> {
  return send<Workspace>("POST", "/api/workspaces", { name, slug: null }, owner);
}

/** Invites someone and accepts on their behalf. */
export async function addMember(
  owner: Person,
  workspace: Workspace,
  member: Person,
  role: Role,
): Promise<void> {
  const invitation = await invite(owner, workspace, member.email, role);

  await send("POST", "/api/invitations/accept", { token: invitation.token }, member);
}

export function invite(
  owner: Person,
  workspace: Workspace,
  email: string,
  role: Role,
): Promise<{ token: string; invitation: { id: string } }> {
  return send("POST", `/api/workspaces/${workspace.slug}/invitations`, { email, role }, owner);
}

export function createCustomer(
  actor: Person,
  workspace: Workspace,
  name = uniqueName("E2E Müşteri"),
): Promise<{ id: string; name: string }> {
  return send(
    "POST",
    `/api/workspaces/${workspace.slug}/customers`,
    {
      name,
      email: null,
      phone: null,
      company: null,
      status: "Active",
      notes: null,
    },
    actor,
  );
}

export function createTicket(
  actor: Person,
  workspace: Workspace,
  customerId: string,
  subject = uniqueName("E2E talebi"),
): Promise<{ id: string; number: number; subject: string }> {
  return send(
    "POST",
    `/api/workspaces/${workspace.slug}/tickets`,
    {
      customerId,
      subject,
      description: "Uçtan uca test için açıldı.",
      priority: "Medium",
      assignedUserId: null,
    },
    actor,
  );
}

/** The caller's own notification feed in one workspace. */
export function listNotifications(
  actor: Person,
  slug: string,
): Promise<{ items: { type: string; isRead: boolean }[]; unreadCount: number }> {
  return send("GET", `/api/workspaces/${slug}/notifications`, undefined, actor);
}

/** Attaches a small text file to a ticket, the way the upload form does. */
export async function uploadAttachment(
  actor: Person,
  workspace: Workspace,
  ticketId: string,
  fileName = "kurulum-notlari.txt",
): Promise<{ id: string; fileName: string }> {
  const form = new FormData();
  form.append("file", new Blob(["Kurulum notları"], { type: "text/plain" }), fileName);

  const response = await fetch(
    `${API_URL}/api/workspaces/${workspace.slug}/tickets/${ticketId}/attachments`,
    { method: "POST", headers: { Authorization: `Bearer ${actor.accessToken}` }, body: form },
  );

  if (!response.ok) {
    throw new Error(`Dosya yüklenemedi → ${response.status}: ${await response.text()}`);
  }

  return (await response.json()) as { id: string; fileName: string };
}

async function send<T = unknown>(
  method: string,
  path: string,
  body: unknown,
  actor?: Person,
): Promise<T> {
  const headers: Record<string, string> = { "Content-Type": "application/json" };

  if (actor !== undefined) {
    headers["Authorization"] = `Bearer ${actor.accessToken}`;
  }

  const response = await fetch(`${API_URL}${path}`, {
    method,
    headers,
    body: body === undefined ? undefined : JSON.stringify(body),
  });

  if (!response.ok) {
    throw new Error(`${method} ${path} → ${response.status}: ${await response.text()}`);
  }

  const text = await response.text();

  return (text === "" ? undefined : JSON.parse(text)) as T;
}
