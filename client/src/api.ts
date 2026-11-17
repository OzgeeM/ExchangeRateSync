// Types mirror the JSON returned by the ASP.NET Core API (camelCase, enums as strings).

export type SyncStatus = 'Running' | 'Succeeded' | 'Failed'

export type SyncTrigger = 'Scheduled' | 'Manual'

export interface RateItem {
    currency: string
    rate: number
}

export interface LatestRatesResponse {
    baseCurrency: string
    date: string | null
    rates: RateItem[]
}

export interface HistoryPoint {
    date: string
    rate: number
}

export interface RateHistoryResponse {
    baseCurrency: string
    currency: string
    points: HistoryPoint[]
}

export interface SyncLogItem {
    id: number
    startedAtUtc: string
    finishedAtUtc: string | null
    durationSeconds: number | null
    status: SyncStatus
    trigger: SyncTrigger
    insertedCount: number
    updatedCount: number
    attemptCount: number
    errorMessage: string | null
}

interface ProblemDetails {
    title?: string
    detail?: string
    status?: number
}

async function request<T>(url: string, init?: RequestInit): Promise<T> {
    const response = await fetch(url, init)

    if (!response.ok) {
        let message = `Request failed with status ${response.status}.`

        try {
            const problem = (await response.json()) as ProblemDetails
            message = problem.detail ?? problem.title ?? message
        } catch {
            // The error body is not JSON; keep the default message.
        }

        throw new Error(message)
    }

    return (await response.json()) as T
}

export function getLatestRates(): Promise<LatestRatesResponse> {
    return request<LatestRatesResponse>('/api/rates/latest')
}

export function getRateHistory(currency: string, days: number): Promise<RateHistoryResponse> {
    return request<RateHistoryResponse>(
        `/api/rates/${encodeURIComponent(currency)}/history?days=${days}`,
    )
}

export function getSyncLogs(take = 20): Promise<SyncLogItem[]> {
    return request<SyncLogItem[]>(`/api/sync/logs?take=${take}`)
}

export function runSync(): Promise<SyncLogItem> {
    return request<SyncLogItem>('/api/sync', { method: 'POST' })
}