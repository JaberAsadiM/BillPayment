package com.persianswitch.smartpos.aidl;

import android.os.Parcel;
import android.os.Parcelable;

import java.util.Date;

public class POSTransactionInquiryResult implements Parcelable {

  private int status;
  private String statusMessage;
  private String maskedCard;
  private int transactionStan;
  private String RRN;
  private Date transactionDate;
  private String extraData;

  public POSTransactionInquiryResult()
  {

  }

  public POSTransactionInquiryResult(Parcel in) {
    status = in.readInt();
    statusMessage = in.readString();
    maskedCard = in.readString();
    transactionStan = in.readInt();
    RRN = in.readString();
    transactionDate = (Date) in.readSerializable();
    extraData = in.readString();

  }

  @Override
  public void writeToParcel(Parcel dest, int flags) {
    dest.writeInt(status);
    dest.writeString(statusMessage);
    dest.writeString(maskedCard);
    dest.writeInt(transactionStan);
    dest.writeString(RRN);
    dest.writeSerializable(transactionDate);
    dest.writeString(extraData);
  }

  @Override
  public int describeContents() {
    return 0;
  }

  public static final Creator<POSTransactionInquiryResult> CREATOR = new Creator<POSTransactionInquiryResult>() {
    @Override
    public POSTransactionInquiryResult createFromParcel(Parcel in) {
      return new POSTransactionInquiryResult(in);
    }

    @Override
    public POSTransactionInquiryResult[] newArray(int size) {
      return new POSTransactionInquiryResult[size];
    }
  };

  public int getStatus() {
    return status;
  }

  public void setStatus(int status) {
    this.status = status;
  }

  public String getStatusMessage() {
    return statusMessage;
  }

  public void setStatusMessage(String statusMessage) {
    this.statusMessage = statusMessage;
  }

  public String getMaskedCard() {
    return maskedCard;
  }

  public void setMaskedCard(String maskedCard) {
    this.maskedCard = maskedCard;
  }

  public int getTransactionStan() {
    return transactionStan;
  }

  public void setTransactionStan(int transactionStan) {
    this.transactionStan = transactionStan;
  }

  public Date getTransactionDate() {
    return transactionDate;
  }

  public void setTransactionDate(Date transactionDate) {
    this.transactionDate = transactionDate;
  }

  public String getRRN() {
    return RRN;
  }

  public void setRRN(String RRN) {
    this.RRN = RRN;
  }
}
