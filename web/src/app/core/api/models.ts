export interface Profile {
  societyId: string;
  societyName: string;
  flatId: string | null;
  tower: string;
  flat: string;
  isListedFlat: boolean;
}

export interface Me {
  id: string;
  phone: string;
  name: string | null;
  isPlatformAdmin: boolean;
  profile: Profile | null;
}

export interface AuthResponse {
  accessToken: string;
  accessTokenExpiresAt: string;
  user: Me;
}

export interface OtpRequested {
  expiresInSeconds: number;
  resendAfterSeconds: number;
  developmentCode: string | null;
}

export interface SaveProfileRequest {
  name: string;
  societyId: string;
  flatId: string | null;
  manualTower: string | null;
  manualFlat: string | null;
  consent: boolean;
}

export interface Society {
  id: string;
  name: string;
  city: string;
}

export interface Flat {
  id: string;
  number: string;
}

export interface Tower {
  id: string;
  name: string;
  flats: Flat[];
}

export interface AdminSociety {
  id: string;
  name: string;
  city: string;
  isActive: boolean;
  towers: number;
  flats: number;
  residents: number;
}

export interface ImportFlatsResult {
  towersCreated: number;
  flatsCreated: number;
  flatsAlreadyListed: number;
  errors: { line: number; message: string }[];
}

export type BusinessStatus = 'Pending' | 'Active' | 'Rejected' | 'Deactivated';

export interface Category {
  id: string;
  name: string;
  sortOrder: number;
  isActive: boolean;
}

export interface BusinessType {
  id: string;
  name: string;
  orderingEnabled: boolean;
  sortOrder: number;
  isActive: boolean;
  categories: Category[];
}

export interface SaveBusinessRequest {
  businessTypeId: string;
  categoryId: string;
  name: string;
  description: string | null;
  upiId: string | null;
  fssaiNumber: string | null;
  whatsAppPhone: string | null;
}

export interface BusinessSettings {
  opensAt: string;
  closesAt: string;
  acceptOrdersUntil: string | null;
  isOpen: boolean;
  minOrderValue: number;
  deliveryCharge: number;
}

export interface Photo {
  id: string;
  url: string;
}

export interface OwnerBusiness {
  id: string;
  name: string;
  description: string | null;
  businessTypeId: string;
  businessTypeName: string;
  orderingEnabled: boolean;
  categoryId: string;
  categoryName: string;
  upiId: string | null;
  fssaiNumber: string | null;
  whatsAppPhone: string;
  status: BusinessStatus;
  statusNote: string | null;
  settings: BusinessSettings;
  isOpenNow: boolean;
  photos: Photo[];
}

export interface SaveItemRequest {
  name: string;
  description: string | null;
  price: number;
  isVeg: boolean | null;
  dailyLimit: number | null;
  isAvailableToday: boolean;
  isHidden: boolean;
  sortOrder: number;
}

export interface OwnerItem extends SaveItemRequest {
  id: string;
  photoUrl: string | null;
  soldToday: number;
  isSoldOut: boolean;
}

export interface AdminBusiness {
  id: string;
  name: string;
  description: string | null;
  societyId: string;
  societyName: string;
  ownerName: string;
  ownerPhone: string;
  ownerAddress: string;
  businessTypeName: string;
  categoryName: string;
  upiId: string | null;
  fssaiNumber: string | null;
  status: BusinessStatus;
  statusNote: string | null;
  createdAt: string;
  photoUrls: string[];
}

export interface CategoryChip {
  id: string;
  name: string;
  count: number;
}

export interface BusinessCard {
  id: string;
  name: string;
  description: string | null;
  businessTypeName: string;
  categoryId: string;
  categoryName: string;
  orderingEnabled: boolean;
  isOpenNow: boolean;
  opensAt: string;
  closesAt: string;
  coverPhotoUrl: string | null;
}

export interface Directory {
  categories: CategoryChip[];
  businesses: BusinessCard[];
}

export interface MenuItem {
  id: string;
  name: string;
  description: string | null;
  price: number;
  isVeg: boolean | null;
  photoUrl: string | null;
  isSoldOut: boolean;
  remainingToday: number | null;
}

export interface BusinessPage {
  id: string;
  name: string;
  description: string | null;
  businessTypeName: string;
  categoryName: string;
  orderingEnabled: boolean;
  isOpenNow: boolean;
  isAcceptingOrders: boolean;
  opensAt: string;
  closesAt: string;
  acceptOrdersUntil: string | null;
  minOrderValue: number;
  deliveryCharge: number;
  fssaiNumber: string | null;
  acceptsUpi: boolean;
  whatsAppUrl: string;
  isOwnerPreview: boolean;
  photoUrls: string[];
  items: MenuItem[];
}

export type OrderStatus =
  | 'Placed'
  | 'Accepted'
  | 'Preparing'
  | 'OutForDelivery'
  | 'Delivered'
  | 'Rejected'
  | 'Expired'
  | 'CancelledByCustomer'
  | 'CancelledByOwner';

export type PaymentMethod = 'Cod' | 'Upi';

export type PaymentStatus =
  'Pending' | 'CustomerMarkedPaid' | 'Confirmed' | 'Collected' | 'Disputed' | 'Refunded';

export type PaymentAction = 'Confirm' | 'Collect' | 'Dispute' | 'Refund';

export interface UpiPayment {
  upiId: string;
  payeeName: string;
  amount: number;
  link: string;
}

export interface Payment {
  status: PaymentStatus;
  utr: string | null;
  markedPaidAt: string | null;
  settledAt: string | null;
  refundedAt: string | null;
  note: string | null;
  upi: UpiPayment | null;
}

export interface PlaceOrderRequest {
  businessId: string;
  items: { itemId: string; quantity: number }[];
  note: string | null;
  preferredDay: 'today' | 'tomorrow' | null;
  preferredTime: string | null;
  paymentMethod: PaymentMethod;
  idempotencyKey: string;
}

export interface OrderLine {
  name: string;
  price: number;
  quantity: number;
  isVeg: boolean | null;
  lineTotal: number;
}

export interface OrderStep {
  status: OrderStatus;
  reason: string | null;
  at: string;
}

export interface Order {
  id: string;
  orderNo: number;
  businessId: string;
  businessName: string;
  businessWhatsAppUrl: string;
  status: OrderStatus;
  placedAt: string;
  expiresAt: string | null;
  preferredTime: string | null;
  note: string | null;
  customerName: string;
  customerPhone: string;
  tower: string;
  flat: string;
  items: OrderLine[];
  subtotal: number;
  deliveryCharge: number;
  total: number;
  paymentMethod: PaymentMethod;
  payment: Payment;
  history: OrderStep[];
}

export interface OrderSummary {
  id: string;
  orderNo: number;
  businessId: string;
  businessName: string;
  status: OrderStatus;
  placedAt: string;
  expiresAt: string | null;
  preferredTime: string | null;
  customerName: string;
  tower: string;
  flat: string;
  itemsText: string;
  total: number;
  paymentMethod: PaymentMethod;
  paymentStatus: PaymentStatus;
}

export type OwnerOrderTab = 'New' | 'Preparing' | 'OutForDelivery' | 'Done';

export interface OwnerOrders {
  counts: Record<OwnerOrderTab, number>;
  orders: OrderSummary[];
}

export interface PrepLine {
  name: string;
  isVeg: boolean | null;
  toPrepare: number;
  awaitingAcceptance: number;
}

export interface PrepSummary {
  ordersToPrepare: number;
  ordersAwaitingAcceptance: number;
  items: PrepLine[];
}
