import type { Metadata } from "next";
import { SignedInSummary } from "@/features/auth/signed-in-summary";

/*
  Faz 03 karşılama ekranı.

  Yalnızca oturumun gerçekten kurulduğunu ve sayfa yenilendiğinde refresh
  çerezinden geri geldiğini gösteriyor. Çalışma alanı seçimi ve gerçek
  dashboard Faz 04 ve Faz 09'da geliyor; sahte bir pano göstermek ilerleme
  izlenimi verir ama ilerleme değildir.
*/

export const metadata: Metadata = {
  title: "Genel bakış",
};

export default function HomePage() {
  return <SignedInSummary />;
}
