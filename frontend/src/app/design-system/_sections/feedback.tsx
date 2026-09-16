"use client";

import {
  CircleAlertIcon,
  CircleCheckIcon,
  InfoIcon,
  MoreHorizontalIcon,
  TriangleAlertIcon,
} from "lucide-react";
import { toast } from "sonner";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogClose,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "@/components/ui/dialog";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { Tooltip, TooltipContent, TooltipTrigger } from "@/components/ui/tooltip";
import {
  CustomerStatusBadge,
  MembershipRoleBadge,
  TaskStatusBadge,
  TicketPriorityBadge,
  TicketStatusBadge,
} from "@/components/product/status-badge";
import { TASK_STATUSES, TICKET_PRIORITIES, TICKET_STATUSES } from "@/types/domain";
import { ShowcaseSection, Specimen, Surface } from "../_components/showcase-section";

/*
  Geri bildirim: rozetler, durum göstergeleri, uyarılar, bildirimler,
  açılır menü, diyalog ve ipucu.
*/

function FeedbackSections() {
  return (
    <>
      <ShowcaseSection
        id="durumlar"
        title="Durum göstergeleri"
        description="Domain kodu İngilizce kalır, kullanıcıya Türkçe etiket gösterilir. Renk tek başına anlam taşımaz; her rozet metin de içerir."
      >
        <Surface className="flex flex-col gap-5">
          <Specimen label="Talep durumu">
            <div className="flex flex-wrap items-center gap-2">
              {TICKET_STATUSES.map((status) => (
                <TicketStatusBadge key={status} status={status} />
              ))}
            </div>
          </Specimen>

          <Specimen label="Talep önceliği">
            <div className="flex flex-wrap items-center gap-2">
              {TICKET_PRIORITIES.map((priority) => (
                <TicketPriorityBadge key={priority} priority={priority} />
              ))}
            </div>
          </Specimen>

          <Specimen label="Görev durumu">
            <div className="flex flex-wrap items-center gap-2">
              {TASK_STATUSES.map((status) => (
                <TaskStatusBadge key={status} status={status} />
              ))}
            </div>
          </Specimen>

          <Specimen label="Müşteri durumu ve ekip rolü">
            <div className="flex flex-wrap items-center gap-2">
              <CustomerStatusBadge status="Active" />
              <CustomerStatusBadge status="Inactive" />
              <MembershipRoleBadge role="Owner" />
              <MembershipRoleBadge role="Admin" />
              <MembershipRoleBadge role="Agent" />
              <MembershipRoleBadge role="Viewer" />
            </div>
          </Specimen>
        </Surface>
      </ShowcaseSection>

      <ShowcaseSection
        id="rozetler"
        title="Rozetler"
        description="Hap biçimi yalnızca anlamsal olarak uygun olduğunda kullanılır: sayaç ve filtre çipi. Durum rozetleri hap değildir."
      >
        <Surface className="flex flex-col gap-5">
          <Specimen label="Varyantlar">
            <div className="flex flex-wrap items-center gap-2">
              <Badge>Yeni</Badge>
              <Badge variant="secondary">Taslak</Badge>
              <Badge variant="outline">Arşivlendi</Badge>
              <Badge variant="destructive">Süresi doldu</Badge>
            </div>
          </Specimen>

          <Specimen label="Sayaç (hap biçimi uygun)">
            <div className="flex flex-wrap items-center gap-2">
              <Badge shape="pill" variant="secondary">
                12
              </Badge>
              <Badge shape="pill" variant="secondary">
                248
              </Badge>
              <Badge shape="pill">3</Badge>
            </div>
          </Specimen>
        </Surface>
      </ShowcaseSection>

      <ShowcaseSection
        id="uyarilar"
        title="Uyarılar"
        description="İnce kenarlık ve açık zemin kullanılır; dolgu uyarı bir operasyon ekranında gereğinden fazla bağırır. Hata metni ne olduğunu ve nasıl düzeltileceğini söyler."
      >
        <div className="flex flex-col gap-3">
          <Alert variant="info">
            <InfoIcon />
            <AlertTitle>Çalışma alanı deneme sürümünde</AlertTitle>
            <AlertDescription>
              Deneme süresi 12 gün sonra doluyor. Ayarlar ekranından planı yükseltebilirsiniz.
            </AlertDescription>
          </Alert>

          <Alert variant="success">
            <CircleCheckIcon />
            <AlertTitle>Davet gönderildi</AlertTitle>
            <AlertDescription>
              ayse.demir@kuzeyyazilim.com adresine davet bağlantısı iletildi.
            </AlertDescription>
          </Alert>

          <Alert variant="warning">
            <TriangleAlertIcon />
            <AlertTitle>Üç görevin son tarihi geçti</AlertTitle>
            <AlertDescription>
              Görevler ekranında &quot;Gecikmiş&quot; filtresiyle listeleyebilirsiniz.
            </AlertDescription>
          </Alert>

          <Alert variant="destructive">
            <CircleAlertIcon />
            <AlertTitle>Talep kaydedilemedi</AlertTitle>
            <AlertDescription>
              Bu talep siz düzenlerken başka biri tarafından güncellendi. Sayfayı yenileyip
              değişikliklerinizi tekrar uygulayın.
            </AlertDescription>
          </Alert>
        </div>
      </ShowcaseSection>

      <ShowcaseSection
        id="bildirimler"
        title="Bildirimler"
        description='Bildirim, tamamlanan eylemi eylemin kendisiyle aynı kelimeyle bildirir: "Kaydet" düğmesi "Kaydedildi" bildirimi üretir.'
      >
        <Surface>
          <div className="flex flex-wrap items-center gap-2">
            <Button
              variant="outline"
              onClick={() => {
                toast.success("Müşteri kaydedildi", {
                  description: "Acme Teknoloji müşteri listesine eklendi.",
                });
              }}
            >
              Başarılı bildirim
            </Button>
            <Button
              variant="outline"
              onClick={() => {
                toast.info("Talep atandı", { description: "TLP-1042 Ahmet Yılmaz'a atandı." });
              }}
            >
              Bilgi bildirimi
            </Button>
            <Button
              variant="outline"
              onClick={() => {
                toast.warning("Son tarih yaklaşıyor", {
                  description: "Bu görevin son tarihine iki gün kaldı.",
                });
              }}
            >
              Uyarı bildirimi
            </Button>
            <Button
              variant="outline"
              onClick={() => {
                toast.error("Kayıt silinemedi", {
                  description: "Bu müşteriye bağlı açık talepler var.",
                });
              }}
            >
              Hata bildirimi
            </Button>
          </div>
        </Surface>
      </ShowcaseSection>

      <ShowcaseSection
        id="katmanlar"
        title="Açılır menü, diyalog ve ipucu"
        description="Gölge yalnızca gerçekten üst katmanda duran yüzeyler için kullanılır. Diyalog odağı tuzaklar ve Esc ile kapanır."
      >
        <Surface>
          <div className="flex flex-wrap items-center gap-3">
            <DropdownMenu>
              <DropdownMenuTrigger
                render={
                  <Button variant="outline" size="icon" aria-label="Satır eylemleri">
                    <MoreHorizontalIcon />
                  </Button>
                }
              />
              <DropdownMenuContent align="start">
                <DropdownMenuItem>Düzenle</DropdownMenuItem>
                <DropdownMenuItem>Kopyala</DropdownMenuItem>
                <DropdownMenuItem>Talep oluştur</DropdownMenuItem>
                <DropdownMenuSeparator />
                <DropdownMenuItem variant="destructive">Sil</DropdownMenuItem>
              </DropdownMenuContent>
            </DropdownMenu>

            <Dialog>
              <DialogTrigger render={<Button variant="outline">Müşteriyi arşivle</Button>} />
              <DialogContent className="sm:max-w-md">
                <DialogHeader>
                  <DialogTitle>Müşteriyi arşivle</DialogTitle>
                  <DialogDescription>
                    Acme Teknoloji listelerden kaldırılır. Mevcut talepleri ve görevleri korunur,
                    istediğiniz zaman geri alabilirsiniz.
                  </DialogDescription>
                </DialogHeader>
                <DialogFooter>
                  <DialogClose render={<Button variant="ghost">Vazgeç</Button>} />
                  <DialogClose render={<Button>Arşivle</Button>} />
                </DialogFooter>
              </DialogContent>
            </Dialog>

            <Tooltip>
              <TooltipTrigger render={<Button variant="ghost">İpucu göster</Button>} />
              <TooltipContent>Son 30 günde oluşturulan talepler</TooltipContent>
            </Tooltip>
          </div>
        </Surface>
      </ShowcaseSection>
    </>
  );
}

export { FeedbackSections };
