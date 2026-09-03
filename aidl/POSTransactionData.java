package com.persianswitch.smartpos.aidl;

import android.app.PendingIntent;
import android.os.Parcel;
import android.os.Parcelable;

public class POSTransactionData implements Parcelable {

  PendingIntent pendingIntent;
  int stan;

  public POSTransactionData(PendingIntent pendingIntent, int stan)
  {
    this.pendingIntent = pendingIntent;
    this.stan = stan;
  }

  public POSTransactionData(Parcel in) {
    pendingIntent = in.readParcelable(PendingIntent.class.getClassLoader());
    stan = in.readInt();
  }

  @Override
  public void writeToParcel(Parcel dest, int flags) {
    dest.writeParcelable(pendingIntent,flags);
    dest.writeInt(stan);
  }

  public static final Creator<POSTransactionData> CREATOR = new Creator<POSTransactionData>() {
    @Override
    public POSTransactionData createFromParcel(Parcel in) {
      return new POSTransactionData(in);
    }

    @Override
    public POSTransactionData[] newArray(int size) {
      return new POSTransactionData[size];
    }
  };


  @Override
  public int describeContents() {
    return 0;
  }
}
