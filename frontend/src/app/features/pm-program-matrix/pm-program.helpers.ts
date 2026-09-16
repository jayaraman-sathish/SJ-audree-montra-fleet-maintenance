// Shared, side-effect-free PM master UI rules. No production data is seeded here.
export interface ProgramHeader {
  id?: string;
  programCode: string;
  name: string;
  description: string;
  vehicleModelMasterId: string | null;
  vehicleVariantMasterId: string | null;
  effectiveFrom: string;
  effectiveTo: string | null;
  isActive: boolean;
}
export interface ModelReference {
  id: string; modelCode: string; name: string; isActive: boolean;
}
export interface VariantReference {
  id: string; vehicleModelMasterId: string; variantCode: string; name: string; isActive: boolean;
}
export interface ServiceLevel {
  id?: string; planCode: string; name: string; sequence: number; isActive: boolean;
  usageTriggerCode: string; usageInterval: number | null; usageUnit: string;
  calendarInterval: number | null; calendarUnit: string;
  warningUsage: number; warningDays: number;
  extraUsageConditions?: boolean;
}
export function usageUnit(code: string): string {
  return ({ ODOMETER: 'KM', OPERATING_HOURS: 'HOUR', KWH: 'KWH', NONE: '' } as Record<string, string>)[code] || '';
}
export function unitLabel(code: string): string {
  return ({ KM: 'km', HOUR: 'hours', KWH: 'kWh', DAY: 'days', MONTH: 'months', YEAR: 'years' } as Record<string, string>)[code] || code;
}
export function variantOptions(modelId: string | null, variants: VariantReference[], selectedId?: string | null): VariantReference[] {
  return variants.filter(v => v.vehicleModelMasterId === modelId && (v.isActive || v.id === selectedId));
}
export function programValidation(draft: ProgramHeader, models: ModelReference[], variants: VariantReference[], original?: ProgramHeader): string | null {
  if (!draft.programCode.trim()) return 'Enter a Program Code.';
  if (!draft.name.trim()) return 'Enter a Program Name for the overall maintenance schedule.';
  if (draft.programCode.trim().length > 64) return 'Program Code must be 64 characters or fewer.';
  if (draft.name.trim().length > 200) return 'Program Name must be 200 characters or fewer.';
  if (original && draft.programCode !== original.programCode) return 'The existing Program Code cannot be changed.';
  const model = models.find(m => m.id === draft.vehicleModelMasterId);
  const unchangedScope = !!original && draft.vehicleModelMasterId === original.vehicleModelMasterId && draft.vehicleVariantMasterId === original.vehicleVariantMasterId;
  if (!model) return 'Select a model from Vehicle Model Master; the program name does not assign a model.';
  if (!unchangedScope && !model.isActive) return 'Select an active vehicle model.';
  if (draft.vehicleVariantMasterId) {
    const variant = variants.find(v => v.id === draft.vehicleVariantMasterId);
    if (!variant || variant.vehicleModelMasterId !== model.id) return 'The selected variant must belong to the selected model.';
    if (!unchangedScope && !variant.isActive) return 'Select an active variant.';
  }
  return null;
}
export function looksLikeIntervalName(name: string): boolean {
  return /\bPM[\s-]*\d+(?:\s*K)?\b|\b\d+[\s,]*(?:km|kwh|days?|months?|years?|hours?)\b/i.test(name);
}
export function levelFromApi(level: any): ServiceLevel {
  const triggers: any[] = level.triggers || [];
  const usage = triggers.filter(t => String(t.triggerCode).toUpperCase() !== 'TIME');
  const metric = usage[0];
  const time = triggers.find(t => String(t.triggerCode).toUpperCase() === 'TIME');
  const code = metric ? String(metric.triggerCode).toUpperCase() : 'NONE';
  return {
    id: level.id, planCode: level.planCode, name: level.name,
    sequence: level.sequence, isActive: level.isActive,
    usageTriggerCode: code, usageInterval: metric?.intervalValue ?? null,
    usageUnit: metric?.unitCode || usageUnit(code),
    calendarInterval: time?.intervalValue ?? null,
    calendarUnit: String(time?.unitCode || 'MONTH').toUpperCase(),
    warningUsage: metric?.warningValue ?? 0, warningDays: time?.warningValue ?? 0,
    extraUsageConditions: usage.length > 1 || triggers.filter(t => String(t.triggerCode).toUpperCase() === 'TIME').length > 1
  };
}
export function levelValidation(levels: ServiceLevel[]): string | null {
  if (!levels.length) return 'Add at least one service level.';
  const codes = new Set<string>();
  for (let i = 0; i < levels.length; i++) {
    const l = levels[i]; const prefix = `Service level ${i + 1}: `;
    const code = l.planCode.trim().toUpperCase();
    if (!code) return prefix + 'enter a level code, for example PM-5K.';
    if (codes.has(code)) return prefix + 'the level code is duplicated in this program.';
    codes.add(code);
    if (!l.name.trim()) return prefix + 'enter a service level name.';
    if (!Number.isInteger(Number(l.sequence)) || Number(l.sequence) < 0) return prefix + 'order must be a non-negative whole number.';
    if (l.extraUsageConditions) return prefix + 'this legacy level has more than one usage condition. Its conditions have not been changed; review it separately before saving.';
    if (!l.isActive) continue;
    const metric = l.usageTriggerCode !== 'NONE';
    const calendar = l.calendarInterval !== null && l.calendarInterval !== undefined;
    if (metric && (!usageUnit(l.usageTriggerCode) || l.usageUnit !== usageUnit(l.usageTriggerCode))) return prefix + 'the usage unit does not match the usage basis.';
    if (metric && (l.usageInterval === null || !Number.isFinite(Number(l.usageInterval)) || Number(l.usageInterval) <= 0)) return prefix + 'usage interval must be greater than zero.';
    if (calendar && (!Number.isInteger(Number(l.calendarInterval)) || Number(l.calendarInterval) <= 0)) return prefix + 'calendar interval must be a positive whole number.';
    if (calendar && !['DAY', 'MONTH', 'YEAR'].includes(l.calendarUnit)) return prefix + 'choose days, months or years.';
    if (!metric && !calendar) return prefix + 'enter a usage or calendar interval.';
  }
  return null;
}
export function levelPayload(levels: ServiceLevel[]): any[] {
  return levels.map(l => ({
    id: l.id || null, planCode: l.planCode.trim().toUpperCase(), name: l.name.trim(),
    sequence: Number(l.sequence), isActive: l.isActive,
    usageTriggerCode: l.usageTriggerCode, usageUnit: usageUnit(l.usageTriggerCode),
    usageInterval: l.usageTriggerCode === 'NONE' ? null : l.usageInterval,
    calendarInterval: l.calendarInterval, calendarUnit: l.calendarUnit,
    warningUsage: l.warningUsage, warningDays: l.warningDays
  }));
}
export function intervalDescription(l: ServiceLevel): string {
  const parts: string[] = [];
  if (l.usageTriggerCode !== 'NONE' && l.usageInterval !== null) parts.push(`${Number(l.usageInterval).toLocaleString('en-IN')} ${unitLabel(l.usageUnit)}`);
  if (l.calendarInterval !== null) parts.push(`${l.calendarInterval} ${unitLabel(l.calendarUnit)}`);
  return parts.length ? parts.join(' OR ') + (parts.length > 1 ? ' - whichever comes first' : '') : 'No interval configured';
}
