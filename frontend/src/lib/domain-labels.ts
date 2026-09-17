import type {
  CustomerStatus,
  MembershipRole,
  TaskStatus,
  TicketPriority,
  TicketStatus,
} from "@/types/domain";

/*
  Domain kodlarının Türkçe karşılıkları ve görsel tonları.

  Renk tek başına anlam taşımaz; her rozet metin de içerir. Renk körlüğü olan
  kullanıcılar ayrımı metinden okur (docs/DESIGN_SYSTEM.md bölüm 3).

  Record<...> kullanımı bilinçlidir: domain'e yeni bir durum eklendiğinde
  buradaki eşlemenin eksik kalması derleme hatası üretir.
*/

/** Rozet tonu; docs/DESIGN_SYSTEM.md içindeki anlamsal renk ailelerine karşılık gelir. */
export type Tone = "neutral" | "info" | "accent" | "warning" | "success" | "danger";

export type DomainLabel = {
  label: string;
  tone: Tone;
};

export const ticketStatusLabels: Record<TicketStatus, DomainLabel> = {
  Open: { label: "Açık", tone: "info" },
  InProgress: { label: "Devam Ediyor", tone: "accent" },
  Waiting: { label: "Bekliyor", tone: "warning" },
  Resolved: { label: "Çözüldü", tone: "success" },
  Closed: { label: "Kapalı", tone: "neutral" },
};

export const ticketPriorityLabels: Record<TicketPriority, DomainLabel> = {
  Low: { label: "Düşük", tone: "neutral" },
  Medium: { label: "Orta", tone: "info" },
  High: { label: "Yüksek", tone: "warning" },
  Urgent: { label: "Acil", tone: "danger" },
};

export const taskStatusLabels: Record<TaskStatus, DomainLabel> = {
  Todo: { label: "Yapılacak", tone: "neutral" },
  InProgress: { label: "Devam Ediyor", tone: "accent" },
  Done: { label: "Tamamlandı", tone: "success" },
};

export const customerStatusLabels: Record<CustomerStatus, DomainLabel> = {
  Active: { label: "Aktif", tone: "success" },
  Inactive: { label: "Pasif", tone: "neutral" },
};

/** Sıralama seçeneklerinin görünen adları. */
export const customerSortLabels: Record<
  "RecentlyUpdated" | "NameAscending" | "NameDescending" | "RecentlyCreated",
  string
> = {
  RecentlyUpdated: "Son güncellenen",
  NameAscending: "Ada göre (A-Z)",
  NameDescending: "Ada göre (Z-A)",
  RecentlyCreated: "Son eklenen",
};

export const ticketSortLabels: Record<
  "RecentlyUpdated" | "RecentlyCreated" | "PriorityDescending" | "NumberDescending",
  string
> = {
  RecentlyUpdated: "Son güncellenen",
  RecentlyCreated: "Son açılan",
  PriorityDescending: "Önceliğe göre",
  NumberDescending: "Talep numarası",
};

export const taskSortLabels: Record<
  "DueSoonest" | "RecentlyCreated" | "RecentlyUpdated" | "TitleAscending",
  string
> = {
  DueSoonest: "Son tarihe göre",
  RecentlyCreated: "Son eklenen",
  RecentlyUpdated: "Son güncellenen",
  TitleAscending: "Başlığa göre (A-Z)",
};

/*
  Etkinlik satırlarının Türkçe kalıpları.

  Özne `{0}` yer tutucusuna girer ve kalıbın kendisi cümlenin gerisini taşır.
  Türkçede ek, sözcüğün son ünlüsüne göre değiştiği için "X eklendi" gibi
  sonuna ek almayan biçimler seçildi: "TLP-1042'ye" demek, sayının okunuşuna
  bağlı bir ek gerektirir ve makine bunu doğru üretemez.
*/
export const activityTypeLabels: Record<
  | "CustomerCreated"
  | "CustomerArchived"
  | "CustomerRestored"
  | "TicketCreated"
  | "TicketStatusChanged"
  | "TicketAssigned"
  | "TicketUnassigned"
  | "TicketCommented"
  | "TicketDeleted"
  | "TaskCreated"
  | "TaskCompleted"
  | "TaskDeleted"
  | "MemberInvited"
  | "MemberJoined"
  | "MemberRoleChanged"
  | "MemberRemoved"
  | "AttachmentUploaded"
  | "AttachmentDeleted",
  string
> = {
  CustomerCreated: "müşteri kaydı oluşturdu",
  CustomerArchived: "müşteri kaydını arşivledi",
  CustomerRestored: "müşteri kaydını arşivden çıkardı",
  TicketCreated: "talep açtı",
  TicketStatusChanged: "talebin durumunu değiştirdi",
  TicketAssigned: "talebi atadı",
  TicketUnassigned: "talebin atamasını kaldırdı",
  TicketCommented: "talebe yorum yazdı",
  TicketDeleted: "talebi sildi",
  TaskCreated: "görev oluşturdu",
  TaskCompleted: "görevi tamamladı",
  TaskDeleted: "görevi sildi",
  MemberInvited: "ekibe davet gönderdi",
  MemberJoined: "çalışma alanına katıldı",
  MemberRoleChanged: "bir üyenin rolünü değiştirdi",
  MemberRemoved: "bir üyeyi ekipten çıkardı",
  AttachmentUploaded: "talebe dosya ekledi",
  AttachmentDeleted: "talepten dosya sildi",
};

/** Etkinlik filtresindeki kayıt türü adları. */
export const activitySubjectLabels: Record<
  "Customer" | "Ticket" | "TaskItem" | "Member" | "Attachment",
  string
> = {
  Customer: "Müşteri",
  Ticket: "Talep",
  TaskItem: "Görev",
  Member: "Ekip",
  Attachment: "Dosya",
};

export const membershipRoleLabels: Record<MembershipRole, DomainLabel> = {
  Owner: { label: "Sahip", tone: "accent" },
  Admin: { label: "Yönetici", tone: "info" },
  Agent: { label: "Temsilci", tone: "neutral" },
  Viewer: { label: "İzleyici", tone: "neutral" },
};
