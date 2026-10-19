/**
 * Date bindings (Date namespace)
 * Based on AssemblyScript std/assembly/date.ts implementation
 */
export function createDateBindings() {
  return {
    // Static methods
    now: () => Date.now(),
    UTC: (year: number, month = 0, day = 1, hour = 0, minute = 0, second = 0, millisecond = 0) => {
      if (year >= 0 && year <= 99) year += 1900
      return Date.UTC(year, month, day, hour, minute, second, millisecond)
    },
    parse: (dateString: string) => Date.parse(dateString),
    
    // Constructor wrapper
    create: (epochMillis?: number) => {
      return epochMillis !== undefined ? new Date(epochMillis) : new Date()
    },
    
    // Instance method helpers (operate on timestamp)
    getTime: (ms: number) => ms,
    
    getUTCFullYear: (ms: number) => new Date(ms).getUTCFullYear(),
    getUTCMonth: (ms: number) => new Date(ms).getUTCMonth(),
    getUTCDate: (ms: number) => new Date(ms).getUTCDate(),
    getUTCDay: (ms: number) => new Date(ms).getUTCDay(),
    getUTCHours: (ms: number) => new Date(ms).getUTCHours(),
    getUTCMinutes: (ms: number) => new Date(ms).getUTCMinutes(),
    getUTCSeconds: (ms: number) => new Date(ms).getUTCSeconds(),
    getUTCMilliseconds: (ms: number) => new Date(ms).getUTCMilliseconds(),
    
    getFullYear: (ms: number) => new Date(ms).getFullYear(),
    getMonth: (ms: number) => new Date(ms).getMonth(),
    getDate: (ms: number) => new Date(ms).getDate(),
    getDay: (ms: number) => new Date(ms).getDay(),
    getHours: (ms: number) => new Date(ms).getHours(),
    getMinutes: (ms: number) => new Date(ms).getMinutes(),
    getSeconds: (ms: number) => new Date(ms).getSeconds(),
    getMilliseconds: (ms: number) => new Date(ms).getMilliseconds(),
    getTimezoneOffset: (ms: number) => new Date(ms).getTimezoneOffset(),
    
    // Setters (return new timestamp)
    setTime: (ms: number, newTime: number) => newTime,
    
    setUTCMilliseconds: (ms: number, millis: number) => {
      const d = new Date(ms)
      d.setUTCMilliseconds(millis)
      return d.getTime()
    },
    setUTCSeconds: (ms: number, seconds: number) => {
      const d = new Date(ms)
      d.setUTCSeconds(seconds)
      return d.getTime()
    },
    setUTCMinutes: (ms: number, minutes: number) => {
      const d = new Date(ms)
      d.setUTCMinutes(minutes)
      return d.getTime()
    },
    setUTCHours: (ms: number, hours: number) => {
      const d = new Date(ms)
      d.setUTCHours(hours)
      return d.getTime()
    },
    setUTCDate: (ms: number, day: number) => {
      const d = new Date(ms)
      d.setUTCDate(day)
      return d.getTime()
    },
    setUTCMonth: (ms: number, month: number, day?: number) => {
      const d = new Date(ms)
      if (day !== undefined) d.setUTCMonth(month, day)
      else d.setUTCMonth(month)
      return d.getTime()
    },
    setUTCFullYear: (ms: number, year: number, month?: number, day?: number) => {
      const d = new Date(ms)
      if (month !== undefined && day !== undefined) d.setUTCFullYear(year, month, day)
      else if (month !== undefined) d.setUTCFullYear(year, month)
      else d.setUTCFullYear(year)
      return d.getTime()
    },
    
    setMilliseconds: (ms: number, millis: number) => {
      const d = new Date(ms)
      d.setMilliseconds(millis)
      return d.getTime()
    },
    setSeconds: (ms: number, seconds: number) => {
      const d = new Date(ms)
      d.setSeconds(seconds)
      return d.getTime()
    },
    setMinutes: (ms: number, minutes: number) => {
      const d = new Date(ms)
      d.setMinutes(minutes)
      return d.getTime()
    },
    setHours: (ms: number, hours: number) => {
      const d = new Date(ms)
      d.setHours(hours)
      return d.getTime()
    },
    setDate: (ms: number, day: number) => {
      const d = new Date(ms)
      d.setDate(day)
      return d.getTime()
    },
    setMonth: (ms: number, month: number, day?: number) => {
      const d = new Date(ms)
      if (day !== undefined) d.setMonth(month, day)
      else d.setMonth(month)
      return d.getTime()
    },
    setFullYear: (ms: number, year: number, month?: number, day?: number) => {
      const d = new Date(ms)
      if (month !== undefined && day !== undefined) d.setFullYear(year, month, day)
      else if (month !== undefined) d.setFullYear(year, month)
      else d.setFullYear(year)
      return d.getTime()
    },
    
    // String conversions
    toISOString: (ms: number) => new Date(ms).toISOString(),
    toUTCString: (ms: number) => new Date(ms).toUTCString(),
    toDateString: (ms: number) => new Date(ms).toDateString(),
    toTimeString: (ms: number) => new Date(ms).toTimeString(),
    toString: (ms: number) => new Date(ms).toString(),
    toLocaleString: (ms: number) => new Date(ms).toLocaleString(),
    toLocaleDateString: (ms: number) => new Date(ms).toLocaleDateString(),
    toLocaleTimeString: (ms: number) => new Date(ms).toLocaleTimeString(),
    toJSON: (ms: number) => new Date(ms).toJSON(),
    valueOf: (ms: number) => new Date(ms).valueOf(),
    
    // Legacy methods (deprecated but still supported)
    getYear: (ms: number) => new Date(ms).getFullYear() - 1900,
    setYear: (ms: number, year: number) => {
      const d = new Date(ms)
      d.setFullYear(year < 100 ? year + 1900 : year)
      return d.getTime()
    },
    toGMTString: (ms: number) => new Date(ms).toUTCString(),
  }
}
