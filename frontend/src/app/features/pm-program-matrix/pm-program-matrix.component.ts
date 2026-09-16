import { Component, HostListener, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import * as XLSX from 'xlsx';
import {
  ProgramHeader, ModelReference, VariantReference, ServiceLevel,
  variantOptions, programValidation, looksLikeIntervalName,
  levelFromApi, levelValidation, levelPayload, intervalDescription, usageUnit, unitLabel
} from './pm-program.helpers';

@Component({
  selector: 'app-pm-program-matrix', standalone: true, imports: [CommonModule, FormsModule],
  templateUrl: './pm-program-matrix.component.html',
  styleUrls: ['./pm-program-matrix.component.css']
})
export class PmProgramMatrixComponent implements OnInit {
  programs: ProgramHeader[] = [];
  models: ModelReference[] = [];
  variants: VariantReference[] = [];
  programId = '';
  matrix: any = null;
  levels: ServiceLevel[] = [];
  tasks: any[] = [];
  assignments: any[] = [];
  replacements: any[] = [];
  tab = 'details';
  search = ''; section = ''; action = ''; severity = '';
  bulkLevel = ''; copyFrom = ''; copyTo = '';
  modal = ''; message = ''; modalMessage = ''; noticeKind = 'info';
  loadingPrograms = false; loadingMatrix = false; saving = false;
  referencesReady = false; referenceError = false;
  ladderDirty = false; matrixDirty = false;
  private requestNumber = 0;
  private referenceLoads = 0;
  originalProgram: ProgramHeader | undefined;
  program: ProgramHeader = this.emptyProgram();
  importWorkbook: XLSX.WorkBook | null = null;
  importSheets: string[] = [];
  importSheet = ''; importFilename = '';
  importPreview: any = null;
  readonly calendarUnits = [{ code: 'DAY', name: 'Days' }, { code: 'MONTH', name: 'Months' }, { code: 'YEAR', name: 'Years' }];
  readonly usageUnits = usageUnit;
  readonly showUnit = unitLabel;
  readonly describeInterval = intervalDescription;

  constructor(private h: HttpClient) {}

  ngOnInit(): void {
    this.h.get<ModelReference[]>('/api/pm/vehicle-models').subscribe({
      next: rows => { this.models = rows; this.loadedReference(); },
      error: e => this.referenceFailed(e)
    });
    this.h.get<VariantReference[]>('/api/pm/vehicle-variants').subscribe({
      next: rows => { this.variants = rows; this.loadedReference(); },
      error: e => this.referenceFailed(e)
    });
    this.h.get<any[]>('/api/pm/replacement-rules').subscribe({
      next: rows => this.replacements = rows,
      error: e => this.notify(this.errorMessage(e, 'Unable to load replacement rules.'), 'error')
    });
    this.loadPrograms();
  }
  private loadedReference(): void {
    this.referenceLoads++;
    this.referencesReady = this.referenceLoads === 2 && !this.referenceError;
  }
  private referenceFailed(e: any): void {
    this.referenceError = true;
    this.notify(this.errorMessage(e, 'Vehicle masters could not be loaded. Refresh before editing applicability.'), 'error');
  }
  private emptyProgram(): ProgramHeader {
    return {
      programCode: '', name: '', description: '', vehicleModelMasterId: null,
      vehicleVariantMasterId: null, effectiveFrom: new Date().toISOString(), effectiveTo: null, isActive: true
    };
  }
  get selectedProgram(): ProgramHeader | null { return this.matrix?.program || this.programs.find(p => p.id === this.programId) || null; }
  get programModelName(): string {
    const id = this.selectedProgram?.vehicleModelMasterId;
    return id ? this.models.find(m => m.id === id)?.name || 'Model reference unavailable' : 'Model not assigned';
  }
  get variantScopeLabel(): string {
    const p = this.selectedProgram;
    if (!p?.vehicleModelMasterId) return 'Model must be assigned';
    if (!p.vehicleVariantMasterId) {
      const rows=this.variants.filter(v=>v.vehicleModelMasterId===p.vehicleModelMasterId&&v.isActive);
      return rows.length ? `All configured variants: ${rows.map(v=>v.variantCode).join(', ')}` : 'All variants of this model';
    }
    const v=this.variants.find(v => v.id === p.vehicleVariantMasterId);
    return v ? `${v.variantCode} · ${v.name}` : 'Variant reference unavailable';
  }
  get formVariants(): VariantReference[] { return variantOptions(this.program.vehicleModelMasterId, this.variants, this.program.vehicleVariantMasterId); }
  get formModels(): ModelReference[] { return this.models.filter(m => m.isActive || m.id === this.program.vehicleModelMasterId); }
  get intervalNameHint(): boolean { return looksLikeIntervalName(this.program.name); }
  get hasUnsavedChanges(): boolean { return this.ladderDirty || this.matrixDirty; }
  get hasSavedLevels(): boolean { return this.levels.some(l => !!l.id); }
  get matrixReady(): boolean { return this.hasSavedLevels && !this.ladderDirty && !this.loadingMatrix; }
  get programBusy(): boolean { return this.loadingPrograms || this.loadingMatrix || this.saving; }
  get sections(): string[] { return [...new Set(this.tasks.map(t => String(t.sectionName || 'General')))].sort(); }

  @HostListener('window:beforeunload', ['$event'])
  beforeUnload(event: BeforeUnloadEvent): void {
    if (this.hasUnsavedChanges) { event.preventDefault(); event.returnValue = ''; }
  }
  loadPrograms(): void {
    this.loadingPrograms = true;
    this.h.get<ProgramHeader[]>('/api/pm/programs').subscribe({
      next: rows => {
        this.programs = rows; this.loadingPrograms = false;
        if (!this.programId && rows.length) this.programId = rows[0].id || '';
        if (this.programId) this.loadMatrix();
      }, error: e => { this.loadingPrograms = false; this.notify(this.errorMessage(e, 'Unable to load programs.'), 'error'); }
    });
  }
  changeProgram(id: string): void {
    if (id === this.programId || this.programBusy) return;
    if (this.hasUnsavedChanges && !window.confirm('Discard the unsaved service-level or task-matrix changes and switch program?')) return;
    this.programId = id; this.tab = 'details'; this.message = '';
    this.loadMatrix();
  }
  loadMatrix(): void {
    if (!this.programId) return;
    const id = this.programId; const request = ++this.requestNumber;
    this.loadingMatrix = true; this.matrix = null;
    this.levels = []; this.tasks = []; this.assignments = [];
    this.bulkLevel = ''; this.copyFrom = ''; this.copyTo = '';
    this.ladderDirty = false; this.matrixDirty = false;
    this.h.get<any>(`/api/pm/programs/${id}/service-matrix`).subscribe({
      next: data => {
        if (request !== this.requestNumber || id !== this.programId) return;
        this.matrix = data; this.tasks = data.tasks || []; this.assignments = data.assignments || [];
        this.levels = (data.levels || []).map(levelFromApi);
        this.bulkLevel = this.levels[0]?.id || '';
        this.copyFrom = this.bulkLevel; this.copyTo = this.levels[1]?.id || '';
        this.loadingMatrix = false;
      }, error: e => {
        if (request !== this.requestNumber) return;
        this.loadingMatrix = false; this.notify(this.errorMessage(e, 'Unable to load the selected program.'), 'error');
      }
    });
  }
  selectTab(tab: string): void {
    if (this.programBusy) return;
    this.tab = tab;
  }
  openProgram(edit = false): void {
    if (!this.referencesReady || this.programBusy) return;
    if (this.hasUnsavedChanges) { this.notify('Save the current service levels and matrix before editing or creating a program.', 'error'); return; }
    if (edit && !this.selectedProgram) return;
    this.originalProgram = edit ? { ...this.selectedProgram! } : undefined;
    this.program = this.originalProgram ? { ...this.originalProgram } : this.emptyProgram();
    this.modalMessage = ''; this.modal = 'program';
  }
  onModelChange(): void { this.program.vehicleVariantMasterId = null; this.modalMessage = ''; }
  closeModal(): void { if (!this.saving) { this.modal = ''; this.modalMessage = ''; } }
  saveProgram(): void {
    if (this.saving || !this.referencesReady) return;
    const problem = programValidation(this.program, this.models, this.variants, this.originalProgram);
    if (problem) { this.modalMessage = problem; return; }
    const editing = !!this.originalProgram?.id;
    // Copy metadata, not the whole service-matrix response. No ladder/matrix endpoint is called here.
    const body: ProgramHeader = {
      ...this.program, id: this.originalProgram?.id,
      programCode: this.originalProgram?.programCode || this.program.programCode.trim().toUpperCase(),
      name: this.program.name.trim(), description: this.program.description.trim(),
      vehicleVariantMasterId: this.program.vehicleVariantMasterId || null,
      effectiveFrom: this.originalProgram?.effectiveFrom || this.program.effectiveFrom,
      effectiveTo: this.originalProgram?.effectiveTo || null
    };
    this.saving = true; this.modalMessage = '';
    const request = editing
      ? this.h.put<ProgramHeader>(`/api/pm/programs/${this.originalProgram!.id}`, body)
      : this.h.post<ProgramHeader>('/api/pm/programs', body);
    request.subscribe({
      next: saved => {
        this.saving = false; this.modal = ''; this.programId = saved.id || '';
        if (editing) {
          this.programs = this.programs.map(p => p.id === saved.id ? saved : p);
          if (this.matrix?.program?.id === saved.id) this.matrix = { ...this.matrix, program: saved };
          this.notify('Program details updated. Existing service levels, matrix mappings and vehicle assignments are retained.', 'success');
        } else {
          this.programs = [...this.programs, saved]; this.tab = 'ladder'; this.loadMatrix();
          this.notify('Program created. Add its service levels, then select the required checks in Task Matrix.', 'success');
        }
      }, error: e => { this.saving = false; this.modalMessage = this.errorMessage(e, 'Unable to save the program. Nothing was recreated.'); }
    });
  }
  addLevel(): void {
    if (!this.matrix || this.programBusy) return;
    this.levels.push({ planCode: '', name: '', sequence: (this.levels.length + 1) * 10, isActive: true,
      usageTriggerCode: 'ODOMETER', usageInterval: null, usageUnit: 'KM',
      calendarInterval: null, calendarUnit: 'MONTH', warningUsage: 0, warningDays: 0 });
    this.ladderDirty = true;
  }
  markLadderDirty(): void { this.ladderDirty = true; }
  syncUnit(level: ServiceLevel): void {
    level.usageUnit = usageUnit(level.usageTriggerCode);
    level.usageInterval = null; level.warningUsage = 0; this.ladderDirty = true;
  }
  changeCalendarUnit(level: ServiceLevel): void {
    // Never carry a numeric value across units: 90 days is not 90 months.
    level.calendarInterval = null; level.warningDays = 0; this.ladderDirty = true;
  }
  removeNewLevel(index: number): void {
    if (this.levels[index]?.id) return; // Saved levels are deactivated, not deleted.
    this.levels.splice(index, 1); this.ladderDirty = true;
  }
  discardEdits(): void {
    if (window.confirm('Discard the unsaved changes and reload the saved program?')) this.loadMatrix();
  }
  saveLadder(): void {
    if (this.programBusy) return;
    const error = levelValidation(this.levels);
    if (error) { this.notify(error, 'error'); return; }
    if (this.matrixDirty) { this.notify('Save the task matrix before saving changed service levels.', 'error'); return; }
    this.saving = true;
    this.h.put(`/api/pm/programs/${this.programId}/service-ladder`, levelPayload(this.levels)).subscribe({
      next: () => { this.saving = false; this.notify('Service levels saved. Open Task Matrix to define the work at each level.', 'success'); this.loadMatrix(); },
      error: e => { this.saving = false; this.notify(this.errorMessage(e, 'Unable to save service levels.'), 'error'); }
    });
  }
  filteredTasks(): any[] {
    const q = this.search.trim().toLowerCase();
    return this.tasks.filter(t => (!q || `${t.taskCode} ${t.taskName}`.toLowerCase().includes(q))
      && (!this.section || t.sectionName === this.section) && (!this.action || t.actionCode === this.action)
      && (!this.severity || t.severity === this.severity));
  }
  matrixAction(planId: string | undefined, taskId: string): string {
    if (!planId) return '';
    return this.assignments.find(a => a.maintenancePlanId === planId && a.maintenanceTaskDefinitionId === taskId)?.actionCode || '';
  }
  setMatrixAction(planId: string | undefined, task: any, actionCode: string): void {
    if (!planId || !this.matrixReady || this.saving) return;
    const action=(actionCode||'').trim().toUpperCase();
    const i=this.assignments.findIndex(a=>a.maintenancePlanId===planId&&a.maintenanceTaskDefinitionId===task.id);
    if (!action) { if(i>=0)this.assignments.splice(i,1); }
    else if (['I','M','F','D','T','L','R'].includes(action)) {
      const row={maintenancePlanId:planId,maintenanceTaskDefinitionId:task.id,actionCode:action,sequence:task.sortOrder||((i+1)*10),isMandatory:true};
      if(i>=0)this.assignments[i]={...this.assignments[i],...row}; else this.assignments.push(row);
    }
    this.matrixDirty=true;
  }
  assignVisible(on: boolean): void {
    if (!this.bulkLevel || !this.matrixReady) return;
    for (const task of this.filteredTasks()) this.setMatrixAction(this.bulkLevel, task, on ? task.actionCode : '');
  }
  copyLevel(): void {
    if (!this.copyFrom || !this.copyTo || this.copyFrom === this.copyTo || !this.matrixReady) return;
    const source=this.assignments.filter(a=>a.maintenancePlanId===this.copyFrom);
    for(const row of source){const task=this.tasks.find(t=>t.id===row.maintenanceTaskDefinitionId);if(task)this.setMatrixAction(this.copyTo,task,row.actionCode||task.actionCode);}
  }
  assignmentCount(): number { return this.assignments.length; }
  checksForLevel(id: string | undefined): number { return this.assignments.filter(a => a.maintenancePlanId === id && !!a.actionCode).length; }
  saveMatrix(): void {
    if (!this.matrixReady || this.programBusy) return;
    this.saving = true;
    this.h.put(`/api/pm/programs/${this.programId}/task-matrix`, this.assignments).subscribe({
      next: () => { this.saving = false; this.notify('Service task matrix saved. Blank cells are excluded from technician execution.', 'success'); this.loadMatrix(); },
      error: e => { this.saving = false; this.notify(this.errorMessage(e, 'Unable to save the service task matrix.'), 'error'); }
    });
  }
  actionName(code: string): string {
    return ({ I: 'Inspect', M: 'Measure', F: 'Function Test', D: 'Diagnostic', T: 'Torque / Secure', L: 'Lubricate', R: 'Replace' } as Record<string, string>)[code] || code;
  }
  saveReplacement(row: any): void {
    if (this.saving) return;
    this.saving = true;
    this.h.put(`/api/pm/replacement-rules/${row.id}`, row).subscribe({
      next: () => { this.saving = false; this.notify('Replacement rule saved.', 'success'); },
      error: e => { this.saving = false; this.notify(this.errorMessage(e, 'Unable to save the replacement rule.'), 'error'); }
    });
  }
  downloadMatrix(): void {
    if (!this.matrixReady) return;
    const rows = this.tasks.map(t => {
      const row: any = { Section: t.sectionName, Code: t.taskCode, Task: t.taskName, Action: t.actionCode, 'Spec / Limit': t.specification, Severity: t.severity };
      for (const level of this.levels) row[level.planCode] = this.matrixAction(level.id, t.id);
      return row;
    });
    const ws = XLSX.utils.json_to_sheet(rows); const wb = XLSX.utils.book_new();
    XLSX.utils.book_append_sheet(wb, ws, 'Task Matrix');
    XLSX.writeFile(wb, `${this.selectedProgram?.programCode || 'PM'}_Task_Matrix.xlsx`);
  }
  importSchedule(event: Event): void {
    const input = event.target as HTMLInputElement; const file = input.files?.[0];
    if (!file) return;
    if (!this.matrix || this.hasUnsavedChanges || this.programBusy) { this.notify('Select a program and save any pending changes before importing.', 'error'); input.value = ''; return; }
    if (file.size > 5 * 1024 * 1024) { this.notify('The schedule file must be 5 MB or smaller.', 'error'); input.value = ''; return; }
    const reader = new FileReader();
    reader.onload = () => {
      try {
        this.importWorkbook = XLSX.read(reader.result, { type: 'array' });
        this.importSheets = [...this.importWorkbook.SheetNames]; this.importSheet = '';
        this.importFilename = file.name; this.importPreview = null; this.modalMessage = ''; this.modal = 'import';
        // Deliberately do not infer a platform from the editable program NAME.
      } catch { this.notify('Unable to read the workbook.', 'error'); }
      input.value = '';
    };
    reader.onerror = () => { this.notify('Unable to read the selected file.', 'error'); input.value = ''; };
    reader.readAsArrayBuffer(file);
  }
  previewImport(): void {
    this.importPreview = null; this.modalMessage = '';
    if (!this.importWorkbook || !this.importSheet) return;
    try {
      const rows = XLSX.utils.sheet_to_json(this.importWorkbook.Sheets[this.importSheet], { header: 1, defval: '' }) as any[][];
      const headerIndex = rows.findIndex(r => String(r[0]).trim() === 'Section' && String(r[1]).trim() === 'Code');
      if (headerIndex < 0) throw new Error('Choose a task-matrix sheet with Section and Code headers.');
      const headers = rows[headerIndex]; const levels: any[] = [];
      for (let c = 6; c < headers.length; c++) {
        const parts = String(headers[c] || '').split(/\n/); const code = parts[0].trim();
        if (!code) continue;
        const saved = this.levels.find(l => l.planCode.toUpperCase() === code.toUpperCase());
        const rule = parts.slice(1).join(' ');
        const metric = rule.match(/([\d,]+)\s*(km|kwh|hours?)/i);
        const calendar = rule.match(/(\d+)\s*(days?|months?|years?)/i);
        if (!metric && !calendar && !saved) throw new Error(`Set up service level ${code} first, or include its interval in the matrix column header.`);
        const level: ServiceLevel = saved ? { ...saved } : { planCode: code, name: code, sequence: (levels.length + 1) * 10,
          isActive: true, usageTriggerCode: 'NONE', usageInterval: null, usageUnit: '', calendarInterval: null, calendarUnit: 'MONTH', warningUsage: 0, warningDays: 0 };
        if (metric) {
          level.usageTriggerCode = /hour/i.test(metric[2]) ? 'OPERATING_HOURS' : /kwh/i.test(metric[2]) ? 'KWH' : 'ODOMETER';
          level.usageUnit = usageUnit(level.usageTriggerCode); level.usageInterval = Number(metric[1].replace(/,/g, ''));
        }
        if (calendar) { level.calendarInterval = Number(calendar[1]); level.calendarUnit = /day/i.test(calendar[2]) ? 'DAY' : /year/i.test(calendar[2]) ? 'YEAR' : 'MONTH'; }
        levels.push({ ...level, column: c });
      }
      const invalid = levelValidation(levels); if (invalid) throw new Error(invalid);
      const tasks: any[] = []; const assignments: any[] = []; const codes = new Set<string>();
      for (let r = headerIndex + 1; r < rows.length; r++) {
        const row = rows[r]; if (!row[1] || !row[2]) continue;
        const code = String(row[1]).trim().toUpperCase();
        const sectionName=String(row[0]||'').trim();
        if(code==='RPL'||sectionName.startsWith('Replacement —')) continue;
        if (codes.has(code)) throw new Error(`Duplicate maintenance task code ${code} at spreadsheet row ${r + 1}. Replacement rows belong in Replacement / Lubrication.`);
        codes.add(code);
        const action = String(row[3] || 'I').trim().toUpperCase();
        if (!['I', 'M', 'F', 'D', 'T', 'L', 'R'].includes(action)) throw new Error(`Unrecognized action at row ${r + 1}: ${action}.`);
        tasks.push({ sectionName, taskCode: code, taskName: String(row[2]).trim(), actionCode: action,
          specification: String(row[4] || ''), severity: String(row[5] || ''), unitCode: '', suggestedIssueCode: '', sortOrder: (r - headerIndex) * 10, isActive: true });
        for (const l of levels) {
          const value = String(row[l.column] || '').trim().toUpperCase();
          if (!value || ['-', 'N', 'NO', '0', 'FALSE'].includes(value)) continue;
          if (!['I','M','F','D','T','L','R'].includes(value)) throw new Error(`Invalid service action ${value} at row ${r+1}, ${l.planCode}. Use I, M, F, D, T, L, R or leave blank.`);
          assignments.push({ planCode: l.planCode.toUpperCase(), taskCode: code, actionCode:value, sequence: (r - headerIndex) * 10, isMandatory: true });
        }
      }
      if (!tasks.length) throw new Error('This matrix contains no maintenance tasks.');
      this.importPreview = { levels: levelPayload(levels), tasks, assignments };
    } catch (e: any) { this.modalMessage = e.message || 'Unable to interpret this worksheet.'; }
  }
  confirmImport(): void {
    if (!this.importPreview || this.saving) return;
    this.saving = true;
    this.h.post(`/api/pm/programs/${this.programId}/import-matrix`, this.importPreview).subscribe({
      next: () => { this.saving = false; this.modal = ''; this.tab = 'matrix'; this.notify(`Imported the explicitly selected worksheet: ${this.importSheet}.`, 'success'); this.loadMatrix(); },
      error: e => { this.saving = false; this.modalMessage = this.errorMessage(e, 'Import failed.'); }
    });
  }
  private notify(message: string, kind = 'info'): void { this.message = message; this.noticeKind = kind; }
  private errorMessage(error: any, fallback: string): string {
    return typeof error?.error?.message === 'string' ? error.error.message : fallback;
  }
}
