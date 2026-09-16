import type { Metadata } from "next";
import { IBM_Plex_Mono, IBM_Plex_Sans } from "next/font/google";
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

export default function RootLayout({ children }: LayoutProps<"/">) {
  return (
    <html lang="tr" className={`${ibmPlexSans.variable} ${ibmPlexMono.variable} h-full`}>
      <body className="flex min-h-full flex-col">{children}</body>
    </html>
  );
}
