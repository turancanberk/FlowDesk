"use client";

import { PlusIcon, SearchIcon, Trash2Icon } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import {
  Field,
  FieldControl,
  FieldDescription,
  FieldError,
  FieldLabel,
} from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Textarea } from "@/components/ui/textarea";
import { ShowcaseSection, Specimen, Surface } from "../_components/showcase-section";

/*
  Kontroller: buton, girdi ve form yapısı.
*/

function ControlsSections() {
  return (
    <>
      <ShowcaseSection
        id="butonlar"
        title="Butonlar"
        description="Bir ekranda tek bir birincil eylem bulunur. İkiden fazla birincil buton varsa hiyerarşi bozulmuştur. Basıldığında 1 px aşağı kayar; bunun dışında hareket yoktur."
      >
        <Surface className="flex flex-col gap-5">
          <Specimen label="Varyantlar">
            <div className="flex flex-wrap items-center gap-2">
              <Button>Yeni müşteri</Button>
              <Button variant="outline">Düzenle</Button>
              <Button variant="secondary">Filtrele</Button>
              <Button variant="ghost">İptal</Button>
              <Button variant="destructive">Sil</Button>
              <Button variant="link">Ayrıntılar</Button>
            </div>
          </Specimen>

          <Specimen label="Boyutlar">
            <div className="flex flex-wrap items-center gap-2">
              <Button size="sm">Küçük · 28 px</Button>
              <Button>Varsayılan · 32 px</Button>
              <Button size="lg">Büyük · 36 px</Button>
            </div>
          </Specimen>

          <Specimen label="İkonlu ve yalnızca ikon">
            <div className="flex flex-wrap items-center gap-2">
              <Button>
                <PlusIcon />
                Yeni talep
              </Button>
              <Button variant="outline">
                <SearchIcon />
                Ara
              </Button>
              <Button variant="outline" size="icon" aria-label="Kaydı sil">
                <Trash2Icon />
              </Button>
            </div>
          </Specimen>

          <Specimen label="Devre dışı">
            <div className="flex flex-wrap items-center gap-2">
              <Button disabled>Kaydet</Button>
              <Button variant="outline" disabled>
                Düzenle
              </Button>
            </div>
          </Specimen>
        </Surface>
      </ShowcaseSection>

      <ShowcaseSection
        id="girdiler"
        title="Girdiler"
        description="Yer tutucu etiketin yerine geçmez; yalnızca örnek gösterir. Dekoratif kayan etiket kullanılmaz."
      >
        <div className="grid gap-4 lg:grid-cols-2">
          <Surface className="flex flex-col gap-4">
            <Specimen label="Metin girdisi">
              <div className="flex flex-col gap-1.5">
                <Label htmlFor="ds-input-name">Müşteri adı</Label>
                <Input id="ds-input-name" placeholder="Örn. Acme Teknoloji" />
              </div>
            </Specimen>

            <Specimen label="Devre dışı">
              <div className="flex flex-col gap-1.5">
                <Label htmlFor="ds-input-disabled">Talep numarası</Label>
                <Input
                  id="ds-input-disabled"
                  defaultValue="TLP-1042"
                  disabled
                  className="font-mono"
                />
              </div>
            </Specimen>

            <Specimen label="Çok satırlı">
              <div className="flex flex-col gap-1.5">
                <Label htmlFor="ds-textarea">Açıklama</Label>
                <Textarea id="ds-textarea" rows={3} placeholder="Talebin ayrıntılarını yazın" />
              </div>
            </Specimen>
          </Surface>

          <Surface className="flex flex-col gap-4">
            <Specimen label="Seçim">
              <div className="flex flex-col gap-1.5">
                <Label htmlFor="ds-select">Öncelik</Label>
                <Select
                  items={{ Low: "Düşük", Medium: "Orta", High: "Yüksek", Urgent: "Acil" }}
                  defaultValue="Medium"
                >
                  <SelectTrigger id="ds-select" className="w-full">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="Low">Düşük</SelectItem>
                    <SelectItem value="Medium">Orta</SelectItem>
                    <SelectItem value="High">Yüksek</SelectItem>
                    <SelectItem value="Urgent">Acil</SelectItem>
                  </SelectContent>
                </Select>
              </div>
            </Specimen>

            <Specimen label="Onay kutusu">
              <div className="flex flex-col gap-2">
                <div className="flex items-center gap-2">
                  <Checkbox id="ds-checkbox-1" defaultChecked />
                  <Label htmlFor="ds-checkbox-1" className="font-normal">
                    Çözülen talepleri de göster
                  </Label>
                </div>
                <div className="flex items-center gap-2">
                  <Checkbox id="ds-checkbox-2" />
                  <Label htmlFor="ds-checkbox-2" className="font-normal">
                    Yalnızca bana atananlar
                  </Label>
                </div>
                <div className="flex items-center gap-2">
                  <Checkbox id="ds-checkbox-3" disabled />
                  <Label htmlFor="ds-checkbox-3" className="text-muted-foreground font-normal">
                    Arşivlenmiş kayıtlar
                  </Label>
                </div>
              </div>
            </Specimen>
          </Surface>
        </div>
      </ShowcaseSection>

      <ShowcaseSection
        id="formlar"
        title="Form alanı"
        description="Etiket, girdi, yardımcı metin ve hata mesajı tek bir erişilebilir birim olarak bağlanır. Bağlamayı bileşen yapar; çağıran taraf id ve aria eşleştirmesini elle kurmaz."
      >
        <Surface className="grid gap-5 sm:grid-cols-2">
          <Field described>
            <FieldLabel required>E-posta</FieldLabel>
            <FieldControl>
              <Input type="email" placeholder="ad.soyad@sirket.com" />
            </FieldControl>
            <FieldDescription>Davet bağlantısı bu adrese gönderilir.</FieldDescription>
          </Field>

          <Field invalid described>
            <FieldLabel required>Müşteri adı</FieldLabel>
            <FieldControl>
              <Input defaultValue="" placeholder="Örn. Kuzey Yazılım" />
            </FieldControl>
            <FieldDescription>Listelerde ve talep kayıtlarında görünür.</FieldDescription>
            <FieldError>Müşteri adı zorunludur.</FieldError>
          </Field>
        </Surface>
      </ShowcaseSection>
    </>
  );
}

export { ControlsSections };
