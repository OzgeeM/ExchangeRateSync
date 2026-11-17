import type { SyncLogItem } from '../api'

interface SyncHistoryProps {
    logs: SyncLogItem[] | null
    error: string | null
    syncing: boolean
    onSyncNow: () => void
}

const dateTimeFormatter = new Intl.DateTimeFormat('en-US', {
    dateStyle: 'medium',
    timeStyle: 'short',
})

function formatDuration(seconds: number | null): string {
    return seconds === null ? '—' : `${seconds.toFixed(1)} s`
}

export default function SyncHistory({ logs, error, syncing, onSyncNow }: SyncHistoryProps) {
    return (
        <section className="card">
            <div className="card-header">
                <h2>Sync history</h2>
                <button type="button" className="primary" onClick={onSyncNow} disabled={syncing}>
                    {syncing ? 'Syncing…' : 'Sync now'}
                </button>
            </div>

            {error !== null ? (
                <p className="error">{error}</p>
            ) : logs === null ? (
                <p className="muted">Loading sync history…</p>
            ) : logs.length === 0 ? (
                <p className="muted">No sync runs yet.</p>
            ) : (
                <div className="table-scroll">
                    <table>
                        <thead>
                            <tr>
                                <th>Started</th>
                                <th>Trigger</th>
                                <th>Status</th>
                                <th className="num">Inserted</th>
                                <th className="num">Updated</th>
                                <th className="num">Attempts</th>
                                <th className="num">Duration</th>
                                <th>Error</th>
                            </tr>
                        </thead>
                        <tbody>
                            {logs.map((log) => (
                                <tr key={log.id}>
                                    <td>{dateTimeFormatter.format(new Date(log.startedAtUtc))}</td>
                                    <td>{log.trigger}</td>
                                    <td>
                                        <span className={`badge badge-${log.status.toLowerCase()}`}>{log.status}</span>
                                    </td>
                                    <td className="num">{log.insertedCount}</td>
                                    <td className="num">{log.updatedCount}</td>
                                    <td className="num">{log.attemptCount}</td>
                                    <td className="num">{formatDuration(log.durationSeconds)}</td>
                                    <td className="error-cell" title={log.errorMessage ?? undefined}>
                                        {log.errorMessage ?? '—'}
                                    </td>
                                </tr>
                            ))}
                        </tbody>
                    </table>
                </div>
            )}
        </section>
    )
}