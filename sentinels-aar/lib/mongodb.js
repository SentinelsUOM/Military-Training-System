import mongoose from 'mongoose'

const MONGODB_URI = process.env.MONGODB_URI

if (!MONGODB_URI) {
  throw new Error('MONGODB_URI environment variable is not defined in .env.local')
}

let cached = global.mongoose || { conn: null, promise: null }
global.mongoose = cached

export async function connectDB() {
  // readyState 1 = connected. If the socket dropped (laptop slept, wifi changed,
  // Atlas failover), throw the stale handle away and dial again rather than
  // handing back a dead connection.
  if (cached.conn && mongoose.connection.readyState === 1) return cached.conn
  if (cached.conn) {
    cached.conn = null
    cached.promise = null
  }

  if (!cached.promise) {
    cached.promise = mongoose.connect(MONGODB_URI, {
      bufferCommands: false,
      serverSelectionTimeoutMS: 15000,
    })
  }

  try {
    cached.conn = await cached.promise
  } catch (err) {
    // Never cache a failed connect. Previously the rejected promise stayed in the
    // cache, so every later request replayed the same failure instantly (in ~0.03s,
    // without even retrying) and the dashboard stayed broken until a server restart —
    // even once the network was fine again. Clearing it lets the next request retry.
    cached.promise = null
    throw err
  }

  return cached.conn
}
