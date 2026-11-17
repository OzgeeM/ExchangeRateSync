import { useCallback, useEffect, useState } from 'react'
import {
  getLatestRates,
  getRateHistory,
  getSyncLogs,
  runSync,
  type LatestRatesResponse,
  type RateHistoryResponse,
  type SyncLogItem,
} from './api'
import RateChart from './components/RateChart'
import RatesTable from './components/RatesTable'
import SyncHistory from './components/SyncHistory'

interface Notice {
  type: 'success' | 'error'
  text: string
}

function getErrorMessage(error: unknown): string {
  return error instanceof Error ? error.message : 'An unexpected error occurred.'
}

export default function App() {
  const [latest, setLatest] = useState<LatestRatesResponse | null>(null)
  const [latestError, setLatestError] = useState<string | null>(null)

  const [logs, setLogs] = useState<SyncLogItem[] | null>(null)
  const [logsError, setLogsError] = useState<string | null>(null)

  const [selectedCurrency, setSelectedCurrency] = useState('USD')
  const [days, setDays] = useState(30)
  const [history, setHistory] = useState<RateHistoryResponse | null>(null)
  const [historyError, setHistoryError] = useState<string | null>(null)
  const [historyVersion, setHistoryVersion] = useState(0)

  const [syncing, setSyncing] = useState(false)
  const [notice, setNotice] = useState<Notice | null>(null)

  const loadOverview = useCallback(async () => {
    try {
      setLatest(await getLatestRates())
      setLatestError(null)
    } catch (error) {
      setLatestError(getErrorMessage(error))
    }

    try {
      setLogs(await getSyncLogs())
      setLogsError(null)
    } catch (error) {
      setLogsError(getErrorMessage(error))
    }
  }, [])

  useEffect(() => {
    void loadOverview()
  }, [loadOverview])

  useEffect(() => {
    // Ignore responses that arrive after the user has already picked another currency or range.
    let ignore = false

    getRateHistory(selectedCurrency, days)
      .then((data) => {
        if (!ignore) {
          setHistory(data)
          setHistoryError(null)
        }
      })
      .catch((error: unknown) => {
        if (!ignore) {
          setHistory(null)
          setHistoryError(getErrorMessage(error))
        }
      })

    return () => {
      ignore = true
    }
  }, [selectedCurrency, days, historyVersion])

  function handleSelectCurrency(currency: string) {
    setSelectedCurrency(currency)
    setHistory(null)
    setHistoryError(null)
  }

  function handleDaysChange(value: number) {
    setDays(value)
    setHistory(null)
    setHistoryError(null)
  }

  async function handleSyncNow() {
    setSyncing(true)
    setNotice(null)

    try {
      const result = await runSync()

      setNotice(
        result.status === 'Succeeded'
          ? {
            type: 'success',
            text: `Sync completed: ${result.insertedCount} inserted, ${result.updatedCount} updated.`,
          }
          : { type: 'error', text: `Sync failed: ${result.errorMessage ?? 'Unknown error.'}` },
      )
    } catch (error) {
      setNotice({ type: 'error', text: getErrorMessage(error) })
    } finally {
      setSyncing(false)
      await loadOverview()
      setHistoryVersion((version) => version + 1)
    }
  }

  return (
    <div className="app">
      <header className="app-header">
        <h1>ExchangeRateSync</h1>
        <p className="muted">
          Daily ECB exchange rates, synchronized nightly from the Frankfurter API.
        </p>
      </header>

      {notice !== null && (
        <div className={`notice notice-${notice.type}`} role="status">
          {notice.text}
        </div>
      )}

      {latestError !== null && (
        <div className="notice notice-error" role="alert">
          Could not load rates: {latestError}
        </div>
      )}

      <div className="grid">
        <RatesTable
          data={latest}
          selectedCurrency={selectedCurrency}
          onSelectCurrency={handleSelectCurrency}
        />
        <RateChart
          currency={selectedCurrency}
          days={days}
          onDaysChange={handleDaysChange}
          data={history}
          error={historyError}
        />
      </div>

      <SyncHistory logs={logs} error={logsError} syncing={syncing} onSyncNow={handleSyncNow} />
    </div>
  )
}