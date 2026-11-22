export type UserRole = 'Admin' | 'Secretary' | 'Host';

export type User = {
  id: string;
  username: string;
  displayName: string;
  role: UserRole;
};

export type AuthTokens = {
  accessToken: string;
  refreshToken: string;
  expiresInSeconds: number;
  user: User;
};

export type Category = {
  id: string;
  name: string;
  colorHex: string;
  sortOrder: number;
  isActive: boolean;
  usageCount: number;
};

export type Host = {
  id: string;
  displayName: string;
  username: string;
  role: UserRole;
  isLoginEnabled: boolean;
  isActive: boolean;
  appointmentCount: number;
  titleId: string | null;
  titleName: string | null;
  displayNameWithTitle: string;
};

export type HostTitle = {
  id: string;
  name: string;
  sortOrder: number;
  isActive: boolean;
  hostCount: number;
};

export type MeetingRoomKind = 'HostRoom' | 'Shared';

export type MeetingRoom = {
  id: string;
  name: string;
  kind: MeetingRoomKind;
  hostUserId: string | null;
  hostName: string | null;
  isActive: boolean;
  sortOrder: number;
  appointmentCount: number;
  isHostRoom: boolean;
  kindText: string;
  displayName: string;
};

export type AppointmentParticipant = {
  userId: string;
  displayName: string;
  titleName: string | null;
  isPrimary: boolean;
  displayNameWithTitle: string;
};

export type AppointmentGuestOutcome = {
  attendance: 'Arrived' | 'NoShow' | 'Postponed' | 'Cancelled';
  summary: string | null;
  willRepeat: boolean;
  postNotes: string | null;
  isClosed: boolean;
  closedAtLocal: string | null;
  closedByName: string | null;
  updatedAtLocal: string;
  attendanceText: string;
  repeatText: string;
};

export type AppointmentGuest = {
  guestId: string;
  personId: string;
  firstName: string;
  lastName: string;
  phoneE164: string;
  isPrimary: boolean;
  fullName: string;
  outcome: AppointmentGuestOutcome | null;
};

export type AppointmentGuestInput = {
  personId?: string | null;
  firstName: string;
  lastName: string;
  phone: string;
  email?: string | null;
  organization?: string | null;
  isPrimary?: boolean;
};

export type GuestOutcomeInput = {
  guestId: string;
  attendance: 'Arrived' | 'NoShow' | 'Postponed' | 'Cancelled';
  summary: string | null;
  willRepeat: boolean;
  postNotes: string | null;
  isClosed: boolean;
};

export type AppointmentStatus =
  | 'Scheduled'
  | 'Arrived'
  | 'NoShow'
  | 'Postponed'
  | 'Cancelled'
  | 'Completed';

export type AppointmentMethod = 'Phone' | 'FaceToFace' | 'WhatsApp' | 'Online';

export type AppointmentListItem = {
  id: string;
  startsAtLocal: string;
  endsAtLocal: string;
  durationMinutes: number;
  personFirstName: string;
  personLastName: string;
  phoneE164: string;
  hostUserId: string;
  hostName: string;
  categoryId: string;
  categoryName: string;
  categoryColorHex: string;
  method: AppointmentMethod;
  status: AppointmentStatus;
  isOverdue: boolean;
  isDeleted: boolean;
  studentFullName: string | null;
  personFullName: string;
  roomId: string | null;
  roomName: string | null;
  hostTitleName: string | null;
  participantCount: number;
  guestCount: number;
  participantNames: string | null;
  guestNames: string | null;
  hostNameWithTitle: string;
  guestSummaryText: string;
  participantSummaryText: string;
  timeLabel: string;
  endTimeLabel: string;
  methodText: string;
  statusText: string;
};

export type Institution = {
  name: string;
  email: string;
  phone: string;
  address: string;
  logoUrl: string | null;
};

export type PublicSettings = {
  theme: string;
  defaultReminderMinutes: number;
  workHoursStart: string;
  workHoursEnd: string;
  allowOutOfHoursAppointments: boolean;
  notificationsEnabled: boolean;
  pushEnabled: boolean;
  quietHoursEnabled: boolean;
  quietHoursStart: string;
  quietHoursEnd: string;
  dailyShareTime: string;
};

export type VersionPolicy = {
  minDesktopVersion: string;
  minMobileVersion: string;
  minWebVersion: string;
  latestDesktopVersion: string;
  latestMobileVersion: string;
  latestWebVersion: string;
};

export type ContentManifest = {
  version: string;
  generatedAtUtc: string;
  institution: Institution;
  settings: PublicSettings;
  categories: Category[];
  hosts: Host[];
  titles: HostTitle[];
  rooms: MeetingRoom[];
  features: Record<string, boolean>;
  versionPolicy: VersionPolicy;
};

export type AppointmentOutcomeInput = {
  attendance: 'Arrived' | 'NoShow' | 'Postponed' | 'Cancelled';
  summary: string | null;
  willRepeat: boolean;
  postNotes: string | null;
  isClosed: boolean;
  guestOutcomes?: GuestOutcomeInput[] | null;
};

export type AppointmentInput = {
  firstName: string;
  lastName: string;
  phone: string;
  startsAtLocal: string;
  durationMinutes: number;
  hostUserId: string;
  categoryId: string;
  method: AppointmentMethod;
  description: string | null;
  studentFirstName: string | null;
  studentLastName: string | null;
  reminderOffsetsMinutes: number[];
  ignoreConflicts?: boolean;
  roomId?: string | null;
  participantHostUserIds?: string[] | null;
  guests?: AppointmentGuestInput[] | null;
};

export type AppointmentDetail = {
  id: string;
  startsAtLocal: string;
  endsAtLocal: string;
  durationMinutes: number;
  personFullName: string;
  phoneE164: string;
  hostUserId: string;
  hostName: string;
  categoryId: string;
  categoryName: string;
  categoryColorHex: string;
  method: AppointmentMethod;
  status: AppointmentStatus;
  isClosed: boolean;
  description: string | null;
  studentFullName: string;
  remindersText: string;
  seriesId: string | null;
  seriesIndex: number | null;
  seriesCount: number | null;
  roomId: string | null;
  roomName: string | null;
  roomKind: MeetingRoomKind | null;
  participants: AppointmentParticipant[] | null;
  guests: AppointmentGuest[] | null;
  hasMultipleGuests: boolean;
  hasParticipants: boolean;
  hostNameWithTitle: string;
  roomDisplayName: string;
  guestSummaryText: string;
  participantSummaryText: string;
  dateLabel: string;
  timeRangeLabel: string;
  statusText: string;
};

export type SeriesFrequency = 'Weekly' | 'Biweekly' | 'Monthly';

export type SeriesRequest = {
  frequency: SeriesFrequency;
  count: number;
};

export type SeriesSlot = {
  index: number;
  startsAtLocal: string;
  endsAtLocal: string;
  state: 'Create' | 'Conflict' | 'Past';
  note: string | null;
  dateText: string;
  timeText: string;
  positionText: string;
  stateText: string;
  willCreate: boolean;
};

export type SeriesPlan = {
  slots: SeriesSlot[];
  error: string | null;
  createCount: number;
  skippedCount: number;
  canSave: boolean;
  summary: string;
};

export type SeriesCreateResult = {
  success: boolean;
  error: string | null;
  seriesId: string | null;
  createdCount: number;
  skippedCount: number;
  summary: string;
};

export type SeriesCancelResult = {
  success: boolean;
  error: string | null;
  cancelledCount: number;
  skippedCount: number;
  summary: string;
};

export type ReportSummary = {
  fromLocalDate: string;
  toLocalDate: string;
  total: number;
  completed: number;
  arrived: number;
  noShow: number;
  cancelled: number;
  postponed: number;
  scheduled: number;
  uniquePersons: number;
  returningPersons: number;
  repeatRequested: number;
  averageDurationMinutes: number;
  totalMinutes: number;
  granularity: 'Daily' | 'Monthly';
  points: { label: string; total: number; completed: number; noShow: number }[];
  categories: { categoryId: string; categoryName: string; colorHex: string; total: number }[];
  hosts: { hostUserId: string; hostName: string; total: number; completed: number; cancelled: number }[];
  hours: { hour: number; label: string; total: number }[];
  isEmpty: boolean;
  rangeText: string;
  completionRateText: string;
  noShowRateText: string;
  cancelRateText: string;
  averageDurationText: string;
  bucketLabel: string;
};

export type ReportKind =
  | 'Summary'
  | 'HostPerformance'
  | 'CategoryTrend'
  | 'Attendance'
  | 'Repeat'
  | 'PersonHistory'
  | 'AuditActivity';

export type ReportResult = {
  kind: ReportKind;
  summary: ReportSummary;
  hostPerformance: {
    hostName: string;
    total: number;
    completed: number;
    noShow: number;
    cancelled: number;
    completionRateText: string;
    noShowRateText: string;
    averageDurationText: string;
  }[];
  categoryTrends: {
    categoryId: string;
    categoryName: string;
    colorHex: string;
    total: number;
    points: { label: string; total: number; completed: number; noShow: number }[];
  }[];
  attendance: {
    dateText: string;
    weekdayText: string;
    arrived: number;
    completed: number;
    noShow: number;
    cancelled: number;
    postponed: number;
    scheduled: number;
    total: number;
  }[];
  repeatPersons: {
    personName: string;
    phone: string;
    visitCount: number;
    lastVisitText: string;
    intervalText: string;
  }[];
  personHistory: {
    personName: string;
    phone: string;
    summaryText: string;
    entries: { dateText: string; hostName: string; categoryName: string; statusText: string }[];
  }[];
  auditActivity: {
    total: number;
    actions: { actionName: string; count: number }[];
    entries: { createdAtText: string; userName: string; actionName: string; entityText: string }[];
  } | null;
};
