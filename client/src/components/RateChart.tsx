import {
    CartesianGrid,
    Line,
    LineChart,
    ResponsiveContainer,
    Tooltip,
    XAxis,
    YAxis,
} from 'recharts'
import type { RateHistoryResponse } from '../api'

interface RateChartProps {
    currency: string
    days: number
    onDaysChange: (days: number) => void
    data: RateHistoryResponse | null
    error: string | null
}

const DAY_OPTIONS = [7, 30, 90]

const rateFormatter = new Intl.NumberFormat('en-US', {
    minimumFractionDigits: 4,
    maximumFractionDigits: 6,
})

const axisDateFormatter = new Intl.DateTimeFormat('en-US', {
    month: 'short',
    day: 'numeric',
    timeZone: 'UTC',
})

const tooltipDateFormatter = new Intl.DateTimeFormat('en-US', {
    dateStyle: 'medium',
    timeZone: 'UTC',
})

function toUtcDate(date: string): Date {
    return new Date(`${date}T00:00:00Z`)
}

export default function RateChart({ currency, days, onDaysChange, data, error }: RateChartProps) {
    return (
        <section className="card">
            <div className="card-header">
                <h2>
                    {data?.baseCurrency ?? 'EUR'} → {currency}
                </h2>

                <div className="segmented" role="group" aria-label="Time range">
                    {DAY_OPTIONS.map((option) => (
                        <button
                            key={option}
                            type="button"
                            className={option === days ? 'active' : undefined}
                            aria-pressed={option === days}
                            onClick={() => onDaysChange(option)}
                        >
                            {option}d
                        </button>
                    ))}
                </div>
            </div>

            {error !== null ? (
                <p className="error">{error}</p>
            ) : data === null ? (
                <p className="muted">Loading chart…</p>
            ) : (
                <ResponsiveContainer width="100%" height={320}>
                    <LineChart data={data.points} margin={{ top: 8, right: 16, bottom: 8, left: 8 }}>
                        <CartesianGrid strokeDasharray="3 3" stroke="#e5e7eb" />
                        <XAxis
                            dataKey="date"
                            tickFormatter={(value: string) => axisDateFormatter.format(toUtcDate(value))}
                            minTickGap={24}
                        />
                        <YAxis
                            domain={['auto', 'auto']}
                            tickFormatter={(value: number) => value.toFixed(4)}
                            width={72}
                        />
                        <Tooltip
                            formatter={(value) => [rateFormatter.format(Number(value)), currency]}
                            labelFormatter={(label) => tooltipDateFormatter.format(toUtcDate(String(label)))}
                        />
                        <Line
                            type="monotone"
                            dataKey="rate"
                            stroke="#4f46e5"
                            strokeWidth={2}
                            dot={false}
                            activeDot={{ r: 4 }}
                        />
                    </LineChart>
                </ResponsiveContainer>
            )}
        </section>
    )
}