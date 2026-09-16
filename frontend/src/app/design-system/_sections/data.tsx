"use client";

import * as React from "react";
import { InboxIcon, SearchXIcon } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Pagination } from "@/components/ui/pagination";
import { Skeleton } from "@/components/ui/skeleton";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { EmptyState } from "@/components/product/empty-state";
import { TicketPriorityBadge, TicketStatusBadge } from "@/components/product/status-badge";
import { formatRelativeTime } from "@/lib/format";
import type { TicketPriority, TicketStatus } from "@/types/domain";
import { ShowcaseSection, Specimen, Surface } from "../_components/showcase-section";

/*
  Veri sunumu: tablo, sekmeler, sayfalama, iskelet ve boş durumlar.
*/

type DemoTicket = {
  number: string;
  subject: string;
  customer: string;
  status: TicketStatus;
  priority: TicketPriority;
  assignee: string;
  updatedAt: string;
};

/*
  Vitrin verisi gerçekçi ama kurgusaldır. Lorem ipsum kullanılmaz: anlamsız
  metin, satır yüksekliğinin ve sütun genişliğinin gerçek içerikle nasıl
  çalıştığını gizler.

  Tarihler sabit bir referans noktasına göre üretilir, böylece vitrin her
  açılışta aynı görünür.
*/
const REFERENCE_DATE = new Date("2026-09-16T12:00:00Z");

function hoursAgo(hours: number): string {
  return new Date(REFERENCE_DATE.getTime() - hours * 3_600_000).toISOString();
}

const demoTickets: DemoTicket[] = [
  {
    number: "TLP-1042",
    subject: "Fatura PDF'i indirilemiyor",
    customer: "Acme Teknoloji",
    status: "Open",
    priority: "Urgent",
    assignee: "Ahmet Yılmaz",
    updatedAt: hoursAgo(2),
  },
  {
    number: "TLP-1041",
    subject: "Kullanıcı davetleri ulaşmıyor",
    customer: "Kuzey Yazılım",
    status: "InProgress",
    priority: "High",
    assignee: "Ayşe Demir",
    updatedAt: hoursAgo(9),
  },
  {
    number: "TLP-1038",
    subject: "Rapor ekranında toplamlar hatalı",
    customer: "Nova Lojistik",
    status: "Waiting",
    priority: "Medium",
    assignee: "Mehmet Kaya",
    updatedAt: hoursAgo(30),
  },
  {
    number: "TLP-1034",
    subject: "Mobil görünümde tablo taşıyor",
    customer: "Acme Teknoloji",
    status: "Resolved",
    priority: "Low",
    assignee: "Selin Arslan",
    updatedAt: hoursAgo(76),
  },
  {
    number: "TLP-1029",
    subject: "Eski kayıtların dışa aktarımı",
    customer: "Nova Lojistik",
    status: "Closed",
    priority: "Low",
    assignee: "Ayşe Demir",
    updatedAt: hoursAgo(150),
  },
];

function DataSections() {
  const [page, setPage] = React.useState(1);
  const [selected, setSelected] = React.useState<ReadonlySet<string>>(new Set(["TLP-1041"]));

  function toggleRow(ticketNumber: string) {
    setSelected((current) => {
      const next = new Set(current);
      if (next.has(ticketNumber)) {
        next.delete(ticketNumber);
      } else {
        next.add(ticketNumber);
      }
      return next;
    });
  }

  const allSelected = selected.size === demoTickets.length;

  return (
    <>
      <ShowcaseSection
        id="tablolar"
        title="Tablolar"
        description="Veri tabloları ürünün kimliğidir. Satır 44 px, başlık 36 px. Talep numarası mono yazı tipiyle dizilir; ekranlar arasında aynı kaydı takip etmeyi kolaylaştırır."
      >
        <div className="border-border bg-card overflow-hidden rounded-xl border">
          <Table>
            <TableHeader>
              <TableRow className="hover:bg-muted">
                <TableHead className="w-10">
                  <Checkbox
                    aria-label="Tüm satırları seç"
                    checked={allSelected}
                    onCheckedChange={(checked) => {
                      setSelected(checked ? new Set(demoTickets.map((t) => t.number)) : new Set());
                    }}
                  />
                </TableHead>
                <TableHead className="w-28">Talep</TableHead>
                <TableHead>Konu</TableHead>
                <TableHead>Müşteri</TableHead>
                <TableHead>Durum</TableHead>
                <TableHead>Öncelik</TableHead>
                <TableHead>Atanan kişi</TableHead>
                <TableHead className="text-right">Güncellendi</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {demoTickets.map((ticket) => (
                <TableRow
                  key={ticket.number}
                  data-state={selected.has(ticket.number) ? "selected" : undefined}
                >
                  <TableCell>
                    <Checkbox
                      aria-label={`${ticket.number} numaralı talebi seç`}
                      checked={selected.has(ticket.number)}
                      onCheckedChange={() => {
                        toggleRow(ticket.number);
                      }}
                    />
                  </TableCell>
                  <TableCell className="text-muted-foreground font-mono text-xs">
                    {ticket.number}
                  </TableCell>
                  <TableCell className="text-foreground max-w-xs truncate font-medium">
                    {ticket.subject}
                  </TableCell>
                  <TableCell className="text-muted-foreground">{ticket.customer}</TableCell>
                  <TableCell>
                    <TicketStatusBadge status={ticket.status} />
                  </TableCell>
                  <TableCell>
                    <TicketPriorityBadge priority={ticket.priority} />
                  </TableCell>
                  <TableCell className="text-muted-foreground">{ticket.assignee}</TableCell>
                  <TableCell className="text-muted-foreground text-right">
                    {formatRelativeTime(ticket.updatedAt, REFERENCE_DATE)}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>

          <Pagination page={page} pageSize={25} totalCount={137} onPageChange={setPage} />
        </div>
      </ShowcaseSection>

      <ShowcaseSection
        id="sekmeler"
        title="Sekmeler"
        description="Sekmeler tek bir kaydın farklı yüzlerini gösterir; ayrı sayfalara bölünmesi gereken içerik için kullanılmaz."
      >
        <Surface>
          <Tabs defaultValue="genel">
            <TabsList>
              <TabsTrigger value="genel">Genel bakış</TabsTrigger>
              <TabsTrigger value="talepler">Talepler</TabsTrigger>
              <TabsTrigger value="gorevler">Görevler</TabsTrigger>
              <TabsTrigger value="etkinlik">Etkinlik</TabsTrigger>
            </TabsList>
            <TabsContent value="genel" className="text-muted-foreground pt-4 text-sm">
              Müşteri bilgileri, iletişim kişileri ve notlar bu sekmede toplanır.
            </TabsContent>
            <TabsContent value="talepler" className="text-muted-foreground pt-4 text-sm">
              Bu müşteriye açılmış destek talepleri listelenir.
            </TabsContent>
            <TabsContent value="gorevler" className="text-muted-foreground pt-4 text-sm">
              Müşteriye bağlı görevler ve son tarihleri görünür.
            </TabsContent>
            <TabsContent value="etkinlik" className="text-muted-foreground pt-4 text-sm">
              Kayıt üzerinde yapılan değişikliklerin geçmişi.
            </TabsContent>
          </Tabs>
        </Surface>
      </ShowcaseSection>

      <ShowcaseSection
        id="durumlar-async"
        title="Yükleniyor, boş ve sonuç yok"
        description='Bunlar üç ayrı durumdur ve ayrı metin gösterirler. "Hiç kayıt yok" ile "filtreye uyan kayıt yok" kullanıcıya farklı şeyler söyler; ikisini tek ekrana yıkmak yanlış yönlendirir.'
      >
        <div className="grid gap-4 lg:grid-cols-3">
          <Surface>
            <Specimen label="Yükleniyor">
              <div className="mt-1 flex flex-col gap-3">
                {[0, 1, 2, 3].map((row) => (
                  <div key={row} className="flex items-center gap-3">
                    <Skeleton className="h-4 w-16" />
                    <Skeleton className="h-4 flex-1" />
                    <Skeleton className="h-4 w-20" />
                  </div>
                ))}
              </div>
            </Specimen>
          </Surface>

          <Surface className="p-0">
            <EmptyState
              icon={<InboxIcon />}
              title="Henüz talep yok"
              description="Bu müşteri için ilk destek talebini oluşturduğunuzda burada listelenir."
              action={<Button size="sm">Yeni talep</Button>}
            />
          </Surface>

          <Surface className="p-0">
            <EmptyState
              icon={<SearchXIcon />}
              title="Filtreye uyan kayıt yok"
              description="Arama terimini veya seçtiğiniz durum filtresini değiştirmeyi deneyin."
              action={
                <Button size="sm" variant="outline">
                  Filtreleri temizle
                </Button>
              }
            />
          </Surface>
        </div>
      </ShowcaseSection>
    </>
  );
}

export { DataSections };
