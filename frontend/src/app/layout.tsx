import type { Metadata } from "next";
import { headers } from "next/headers";
import { IBM_Plex_Mono, IBM_Plex_Sans } from "next/font/google";
import { QueryProvider } from "@/components/providers/query-provider";
import { Toaster } from "@/components/ui/sonner";
import "./globals.css";

/*
  IBM Plex Sans arayüzün tamamında kullanılır. IBM Plex Mono yalnızca talep
  numarası ve kayıt kimliği gibi teknik alanlar içindir (docs/DESIGN_SYSTEM.md).
  Türkçe arayüz için latin-ext alt kümesi gereklidir: ş, ğ, ı, İ karakterleri
  temel latin alt kümesinde bulunmaz.
*/
const ibmPlexSans = IBM_Plex_Sans({
  variable: "--font-ibm-plex-sans",
  subsets: ["latin", "latin-ext"],
  weight: ["400", "500", "600"],
  display: "swap",
});

const ibmPlexMono = IBM_Plex_Mono({
  variable: "--font-ibm-plex-mono",
  subsets: ["latin", "latin-ext"],
  weight: ["400", "500"],
  display: "swap",
});

export const metadata: Metadata = {
  title: {
    default: "FlowDesk",
    template: "%s · FlowDesk",
  },
  description:
    "Ekipler için müşteri, talep ve görev yönetimini tek yerde toplayan operasyon platformu.",
};

/*
  Reading the request's headers makes every document render per request, which
  is what lets Next stamp the CSP nonce on its own inline scripts. Prerendered
  HTML has no request and so no nonce, and the policy would block the very
  scripts that start the application (docs/SECURITY.md §14).

  The cost is small here: every page is an authenticated shell whose data is
  fetched in the browser (ADR-0009), so there was never much to prerender.
*/
export default async function RootLayout({ children }: LayoutProps<"/">) {
  await headers();

  return renderDocument(children);
}

function renderDocument(children: React.ReactNode) {
  return (
    <html lang="tr" className={`${ibmPlexSans.variable} ${ibmPlexMono.variable} h-full`}>
      <body className="flex min-h-full flex-col">
        <QueryProvider>{children}</QueryProvider>
        <Toaster />
      </body>
    </html>
  );
}
