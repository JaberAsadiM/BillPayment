package com.persianswitch.smartpos.aidl;

import android.os.Parcel;
import android.os.Parcelable;

public class POSMerchantInfo implements Parcelable {

  private String merchantId;
  private String terminalId;
  private String telepardazCode;
  private String merchantNameEn;
  private String merchantNameFa;
  private int paymentIdStatus;
  private boolean isCreditMenuActive;

  public POSMerchantInfo()
  {

  }

  public POSMerchantInfo(Parcel in) {
    merchantId = in.readString();
    terminalId = in.readString();
    telepardazCode = in.readString();
    merchantNameEn = in.readString();
    merchantNameFa = in.readString();
    paymentIdStatus = in.readInt();
    isCreditMenuActive = in.readByte() != 0;
  }

  @Override
  public void writeToParcel(Parcel dest, int flags) {
    dest.writeString(merchantId);
    dest.writeString(terminalId);
    dest.writeString(telepardazCode);
    dest.writeString(merchantNameEn);
    dest.writeString(merchantNameFa);
    dest.writeInt(paymentIdStatus);
    dest.writeByte((byte) (isCreditMenuActive ? 1 : 0));
  }

  public static final Creator<POSMerchantInfo> CREATOR = new Creator<POSMerchantInfo>() {
    @Override
    public POSMerchantInfo createFromParcel(Parcel in) {
      return new POSMerchantInfo(in);
    }

    @Override
    public POSMerchantInfo[] newArray(int size) {
      return new POSMerchantInfo[size];
    }
  };

  @Override
  public int describeContents() {
    return 0;
  }

  public String getMerchantId() {
    return merchantId;
  }

  public void setMerchantId(String merchantId) {
    this.merchantId = merchantId;
  }

  public String getTerminalId() {
    return terminalId;
  }

  public void setTerminalId(String terminalId) {
    this.terminalId = terminalId;
  }

  public String getTelepardazCode() {
    return telepardazCode;
  }

  public void setTelepardazCode(String telepardazCode) {
    this.telepardazCode = telepardazCode;
  }

  public String getMerchantNameEn() {
    return merchantNameEn;
  }

  public void setMerchantNameEn(String merchantNameEn) {
    this.merchantNameEn = merchantNameEn;
  }

  public String getMerchantNameFa() {
    return merchantNameFa;
  }

  public void setMerchantNameFa(String merchantNameFa) {
    this.merchantNameFa = merchantNameFa;
  }

  public int getPaymentIdStatus() {
    return paymentIdStatus;
  }

  public void setPaymentIdStatus(int paymentIdStatus) {
    this.paymentIdStatus = paymentIdStatus;
  }

  public boolean isCreditMenuActive() {
    return isCreditMenuActive;
  }

  public void setCreditMenuActive(boolean creditMenuActive) {
    isCreditMenuActive = creditMenuActive;
  }
}
