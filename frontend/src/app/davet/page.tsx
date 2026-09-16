import type { Metadata } from "next";
import { AcceptInvitationScreen } from "@/features/team/accept-invitation-screen";

export const metadata: Metadata = {
  title: "Davet",
};

/*
  Davet kabul ekranı.

  Token sorgu parametresinden okunuyor ve istemci bileşenine veriliyor; kabul
  isteği istemci tarafından atılıyor, çünkü kimlik doğrulaması gerektiriyor ve
  access token yalnızca tarayıcı belleğinde (ADR-0009).
*/
export default async function InvitationPage({ searchParams }: PageProps<"/davet">) {
  const params = await searchParams;
  const token = typeof params.token === "string" ? params.token : null;

  return <AcceptInvitationScreen token={token} />;
}
